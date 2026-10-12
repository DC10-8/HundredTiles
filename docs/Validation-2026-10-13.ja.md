# 検証記録 2026-10-13

対象: Windows Steam 1.21.1-f9。ゲームDLLのSHA-256はDF557994A03C93D8BB4A14F80B1FBCC0FB29D547658442445D078D069BCDD9FE。個人設定・ゲームDLL・セーブ・実環境の個人ログは公開ソースへ含めません。

| 検査 | 結果と範囲 |
|---|---|
| 全18プロジェクトのソリューションビルド | 合格。警告・エラー0。 |
| 全体検証スクリプト | 合格。保存形式、欠落・破損・不一致の拒否、ロールバック、グリッド、実ゲームDLLの命令変換。Unityのプレイ検証とは別。 |
| クリーンな二つのソースコピー | 同じ外部入力で6 DLLのSHA-256一致。同一PCの異なるディレクトリでの検査。 |
| 読み込み失敗後の保存 | モックで拒否を確認。既存の拡張データのキーとバイト配列参照が不変。 |
| 起動後の未知パッチ追加 | 隔離した実ゲームで保存前に拒否。取り除くと照合が通り、拡張データのキー数は不変。 |
| 確認済みMOD併用での実ゲーム | 486対象の適用成功。小規模な検証用都市の建設・経路・タクシー乗降・電気水道・地形編集とUndo・全256サービスフェーズ・1→100全開放・言語切り替え・保存都市の再ロードが合格。 |
| 入力セーブの保全 | 再ロード検証前後のSHA-256が一致。ユーザーの元セーブは変更しない。 |
| 終了時例外 | UnifiedUIのExternalButton.OnDestroyでNullReferenceExceptionが1件。無例外の構成として扱わない。 |

互換性の旧例外6 MODとForceRoadConnectにも、元DLLと全メソッドの照合を追加しました。元の許可対象・パッチ種別を越えて適用を広げる変更ではありません。17アセンブリの照合値はReviewed-compatibility.csvへ記載しています。Move It、No Utility Networks、Extra Landscaping Toolsの既存専用照合は別途維持します。

ローカルの詳細証拠: reports/source-audit-full-checks.log、reports/solution-build.log、reports/reproducible-final.log、reports/safety-mock-final.log、reports/safety-native.log、reports/safety-unity.log。これらの個人ログ自体は公開せず、この集計記録と再実行するソースを公開します。

第三者の別PC、大都市の長時間運転、旧81 Tiles独自データ全体の移行、任意のMOD構成は未検証です。リリースは試験公開版のままです。
追補: 修正版による実際のSavePanel保存が成功し、生成した.crp（SHA-256 FDF735C1A2F913E410ABEBE0BDF5115BF63503FC630F405F1425802CEF44749B）を別のゲームプロセスで再読み込みしました。所有・都市オブジェクト・電気水道・経路・地形/Undo・サービス資源が復元され、入力セーブのハッシュは不変でした。保存・再ロードの両プロセス終了時に同じUnifiedUI例外が1件ずつ残りました。無例外の合格とは報告しません。詳細証拠はreports/safety-save-native.log、reports/safety-reload-native.logです。

GitチェックアウトからクリーンにビルドしたDLLも、作業ソースからの配布6 DLLと完全一致しました。公開CIのPublic source checksはコードコミットbfe2410で成功しました。プロプライエタリーなゲームを持たないCIのため、そこで実施したのは純粋/モック検査です。配布DLLのSHA-256はRelease-DLL-hashes.csv、外部入力DLLのSHA-256はBuild-inputs.jsonに記録します。

## FPS Boosterによる互換メソッドの改名（2026-10-13）

実ユーザーログの起動停止はAreaPOBuilder.MainThreadPump.Updateでした。確認済み版の全メソッド照合が、FPS BoosterによるUpdate/LateUpdate/FixedUpdateのFpsBooster名への改名を名前検索で認識していませんでした。既存の意味的ハッシュではこれらの名前を正規化していたため、候補の名前検索にも同じ限定した対応を追加しました。DLLのSHA-256と処理本体の完全な意味的ハッシュ照合は維持します。

AreaPOBuilder、AutoHistoricalPlop、BuildingLimitRemoval、ClearFpsBoost、FreeLotEditor、LoadTimeRemainingの元DLLを基準に、メタデータ番号を並べ替えてFPS名へ改名したメモリー読み込みDLLで全メソッド照合が合格しました。改名に加えてAreaPOBuilder.MainThreadPump.UpdateのILへ命令を追加したDLLは拒否しました。459対象の接続検査も合格しました。実ユーザー都市での修正版起動・全開放は未確認です。

追補: AreaPOBuilder.AreaDragToolとFreeLotEditor.LotToolのOnEnable/OnDisableには、既知のBehaviourRegistry登録/解除を追加した完全な意味的ハッシュ4件も照合します。代替ハッシュの一致時にBehaviourUpdater DLLのSHA-256 0D3DC3B601E3E6157008BD8BF0CB931FC99005D1B3450EB19027FEBB7F4A4C0Eを検証します。両ツールの改名・登録/解除を含むフィクスチャは合格し、OnEnableで解除を呼ぶ異なるフィクスチャは拒否しました。元の処理を一般的に無視する検査ではありません。

## More Network Capacity Renew（Workshop 3818403598）

原版Renewラッパーは旧Harmony.HarmonyInstanceを参照し、現行Harmony環境の実ゲームでTypeLoadExceptionを起こしました。確認済み入力に限定したローカルAPI変換パッチャーを追加しました。対象ハッシュと復元手順はNetwork-capacity-compatibility.ja.mdに記載しています。第三者DLLは配布物へ含めません。

電気・水道の配列とキューを65,024件に対応させ、v1→v2保存移行、循環キューの保持、高番号のノード参照を確認しました。容量縮小、不明な容量、再計算したチェックサムを伴う不正な容量も拒否しました。サービス資源の35,000,980バイトの保存検査を含む既存Verify.ps1が合格しました。

隔離都市では確認済みMOD群とAPI変換済みRenewを有効化し、実際のIUserMod起動・既存保存データの移行・65,024件のネットワークノード配列と6種の電気水道配列を確認しました。未使用ノード65,000へ検査用状態を入れ、電気・水道の更新フェーズが処理することを確認後、全状態を戻しました。65,000個の実ノードを持つ大都市での運用を検証したものではありません。

実際のSavePanel保存が成功し、別プロセスで再ロードしました。セーブSHA-256は8F0CE536560AD8279F250D2702A2769257C8C2041E74D4EC7E6F2BB212917935で、再ロード後も不変でした。所有、拡張部の都市オブジェクト、供給、経路、地形/Undo、サービス資源が復元されました。オプションボタンの99回の開放処理と100タイル所有も合格しました。未知の後付けパッチを保存前に拒否する検査も合格しました。保存・再ロードの終了時に既存のUnifiedUI.GUI.MainPanel/OnDestroyのNullReferenceExceptionが1件ずつ残りました。終了時に残った隔離プロセスを停止しました。通常終了まで無例外とは報告しません。

異なる2つのクリーンソース展開が同じ6 DLLを生成しました。GitチェックアウトだけにSource Link情報が入るハッシュ差も発見し、EnableSourceLink=falseを明示しました。その後、Git公開ツリーのビルドと配布用6 DLLが完全一致しました。19番目のプロジェクトNetworkRenewPatcherを追加し、公開CIにも単体ビルドを追加しました。

非公開の詳細ログ: reports/network-compatibility-checks.log、network-reproducible.log、network-save-native.log、network-save-unity.log、network-reload-native.log、network-reload-unity.log。ユーザーの元セーブは読み書きしていません。大都市・長期運用・全MOD構成の検証は未実施です。
