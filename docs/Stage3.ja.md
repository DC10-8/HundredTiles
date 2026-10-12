# 第3工程：標準シリアライザーと拡張地形・水流保存の接続

## 現在の成果と適用範囲

標準サイズの保存／読み込みを維持しながら、拡張地形・水流データを別キーで保持する
Core保存形式、分割保存、ゲーム用フック、ロード順序制御を実装しました。
SaveBridgeは.NET Framework 3.5向けにビルドしています。
接続先は`SerializerHooks.Specifications()`が返す具体的な7メソッドです。
このメソッドは適用計画を返すだけで、自動的にパッチを適用しません。

**ゲーム内の保存／再ロードは未検証です。**
試験用パッチの自動適用入口は引き続きなく、`CanActivate`はfalseです。
今回の保存対象はraw地形、地形のblock差分、水の高さ・汚染・X/Z流速・フレームです。
100区画の所有情報、他Managerの拡張データ、ゲーム全体の100タイル対応は含みません。
標準地形・水流データを残す方式でも、拡張都市をMODなしで正常に遊べる保証はありません。

## 読み込み順序

ゲーム本体のSimulationManagerとSerializableDataWrapperを確認した結果、
標準`OnLoadData`は各ManagerのLateUpdateDataの後に実行されます。
TerrainManager.LateUpdateDataは既に水流を再開しているため、このコールバックには復元を置きません。

1. SimulationManager.Data.Deserializeのprefixでロードセッションを開始。
2. TerrainManager.Data.Deserializeのraw高さ／block差分の配列参照を1081²のscratch配列へ接続。
3. Terrainのpostfixで読み込んだ標準データを拡張し、既存のlive配列へコピー。
4. WaterSimulation.Data.Deserializeの水流バッファ参照を標準サイズのscratchへ接続。
5. Waterのpostfixで標準水流を拡張して両liveバッファへコピー。新規セルは0。
6. 元のAfterDeserializeを実行。拡張後の地形パッチ数・水流解像度を扱う追加パッチも作成。
7. Managers_UpdateDataのprefixで拡張保存断片を復元し、チェックサム・地形／水流照合・フレーム一致を検証。
8. 水流がReadyForLoadで読み取り利用者が0のことを確認したうえで、同じlive配列へ適用。
9. 水流の両バッファと3枚の存在ビットマップを再構築。高さバッファのポインターは維持。
10. Terrain.UpdateData以降で派生地形・bounds・描画データを再構築し、通常のLateUpdateDataで再開。

拡張保存がない旧データは、旧地形の端の高さを複製して拡張します。
block差分と水流は追加セルに複製しません。
初期移行をDeserializeのpostfixで公開するため、その後の他Managerが初期の平坦地形だけを
参照してしまう期間を避けます。拡張部分の上書きは全標準データが読み終わってからです。

## 保存順序と失敗時の扱い

SimulationManager.Data.Serializeのprefixで、水流が保存フレームへ到達するのを待ち、
BeginRead／EndReadで確定した水流バッファをコピーします。
raw地形・block差分もスナップショットに固定します。
標準シリアライザーへは1081²の投影を渡し、拡張データへは1201²の全体を渡します。
原点や既存座標を変更せず、標準配列のストライドを1081に保ちます。
読み書きのためにlive配列そのものを差し替えません。

標準水流保存の最後のフレーム番号にも同じスナップショットの番号を渡します。
保存時の同期待ちで読むフレーム番号は変更しません。
標準形式が保存しない空セルの汚染／流速やraw高さ0のblock差分は、
地形・水流照合用の投影では標準形式と同じ条件で正規化します。
右端・上端の隣接セルが拡張側だけにある場合も、標準保存側の流速条件を再現します。

保存が失敗した場合のfinalizerは、専用保存キーと全断片を以前の値へ戻します。
初めての保存なら追加したキーを削除します。
通常終了と例外終了のどちらでもメモリー上の保存セッションを解放します。
この巻き戻しはゲーム内のserializable-data dictionaryに対するもので、
ゲーム本体のファイル保存処理のトランザクションを実装したものではありません。

## 保存形式とゲーム側のサイズ制限

SaveCodecの固定形式は17,308,908バイトです。
ヘッダーは形式v1・1201ストライド・原点-8640・タイル幅1920・水流フレーム・標準投影SHA256を保持します。
末尾のSHA256で全ヘッダー／ペイロードの破損を検出します。
未知の形式、異なる寸法、切り詰め、余分なバイト、チェックサム不一致は拒否します。
保存サイズが固定なので、入力に書かれた巨大な要素数を信じて配列を確保しません。

ColossalFramework.IO.DataSerializer.WriteByteArrayは16,711,680バイト以上を拒否します。
そこでSaveTransportはデータを次の3断片へ分割し、52バイトのmanifestと一緒に保存します。

| 保存キー | 内容／サイズ |
| --- | --- |
| HundredTiles.TerrainWater.v1 | manifest、52バイト |
| HundredTiles.TerrainWater.v1.chunk0 | 8,388,608バイト |
| HundredTiles.TerrainWater.v1.chunk1 | 8,388,608バイト |
| HundredTiles.TerrainWater.v1.chunk2 | 531,692バイト |

manifestは断片数・全サイズ・全体SHA256を持ちます。
欠けた断片、混在した断片、manifestなしの断片は拒否し、旧データ扱いに降格しません。
全キーが存在しない場合のみ旧保存として移行します。
同じdictionaryロックの内側で全断片の公開／読み出し／巻き戻しを行います。

## テスト

`Verify.ps1`にSaveChecksとBridgeChecksを追加しました。

- 全1,442,401セルの地形・block差分・水流・流速の完全な往復。
- 標準投影との照合、異なる標準データ／水流フレームの拒否。
- 旧保存からの移行、順序違反・二重完了・二重復元の拒否。
- チェックサム、形式、寸法、断片不足・混在・manifest不足の拒否。
- 各断片がゲーム本体のサイズ制限未満に収まること。
- 本番RuntimeSaveBridgeのソースを模擬Managerと組み合わせ、保存／復元フックのライフサイクルを実行。
- 保存失敗で以前のmanifestと全断片が復元されること。
- 両水流バッファ、存在ビットマップ、停止状態、live配列ポインター維持を確認。
- 破損データは拡張側の上書き前に拒否されること。
- ゲーム本体の4シリアライザーILに対する8箇所の投影／フレーム呼び出しの注入。
- 分岐ラベルと例外終端の保持。注入箇所の欠落・重複を拒否。

Harmony整数変換の計画も、AfterDeserialize・RefreshPatchFlatness・UpdateData・LateUpdateDataへ広げ、
合計21メソッド／119命令を検証しました。
SerializerのILテストではゲーム固有の配列型をfixtureの型で代用しています。
BridgeChecksは模擬Manager上の実行であり、Unity／Mono上の動作確認ではありません。

## 残る接続

地形のサンプリング、地形編集、TerrainModify、TerrainArea、SimulationStepなどの残りの
固定ストライド・境界を拡張し、初期化／再構築処理全体をそろえる必要があります。
GameAreaManagerの100区画の購入・所有情報・標準投影と独自保存も次の対象です。
その後に、他の空間グリッドとの整合、パッチの一括導入と失敗時解除、
試験都市でのゲーム内保存・再ロードを検証します。
