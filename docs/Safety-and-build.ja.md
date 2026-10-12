# 安全性と再現検証の状況

本MODは試験公開版です。重要な都市の唯一のセーブへの使用を推奨できる段階ではありません。元の都市を外部へバックアップし、コピーを別名で保存して検証してください。ビルドできること、パッチの照合に通ること、実ゲームで限られた動作が成功することは、それぞれ異なる検証です。

## コメントの指摘と対応

| 指摘 | 対応と限界 |
|---|---|
| TrainDepartureSequencerへの依存 | 実行時依存ではなく、Verify.ps1のHarmony参照に残った別プロジェクトのパスでした。削除し、HarmonyDirectoryを引数化しました。 |
| 特定PCの逆コンパイルツールへの依存 | 検査用Mono.CecilをNuGet 0.11.6へ固定しました。個人フォルダー参照を除去しました。 |
| 再現可能なビルドがない | SDK 10.0.401、NuGet.Config、packages.lock.json、決定的コンパイルとPathMap、全18プロジェクトのソリューションを追加しました。異なるディレクトリでクリーンビルドした6 DLLのSHA-256一致を検査します。同一PCでの検査であり、第三者の別PCでの再現成功は未確認です。 |
| MOD競合 | 所有者名だけで許可していた旧互換分岐を除去し、確認済みDLLとパッチ・ヘルパーの意味的ハッシュを照合します。保存直前にも対象メソッドを検査し、未知の追加パッチや本MODのパッチ除去を検出すると保存を拒否します。全ゲームメソッド・全MODの監視ではありません。 |
| 読み込み失敗後のセーブ破損 | 読み込み例外を記録し、その状態での保存を拒否します。次の都市読み込みまで拒否を保持します。破損した拡張データを無視して都市を保存する機能はありません。 |
| 実ゲーム検証が不完全 | 隔離した都市で検証を行い、静的・モック検査と区別して結果を記録します。大都市での長時間運転や全MOD組み合わせの安全性は未確認です。 |

## 開発・検証の入口

Windows、.NET SDK 10.0.401、正規に導入した対象ゲームのManagedフォルダー、Cities HarmonyのCitiesHarmony.API.dllとCitiesHarmony.Harmony.dllが必要です。ゲームDLLや依存MODのDLLは公開ソースに含めません。

```powershell
./Build.ps1 -GameManaged 'D:\your-game\Cities_Data\Managed' -HarmonyDirectory 'D:\your-harmony'
./Verify.ps1 -GameAssembly 'D:\your-game\Cities_Data\Managed\Assembly-CSharp.dll' -HarmonyDirectory 'D:\your-harmony'
./Verify-Reproducible.ps1 -GameManaged 'D:\your-game\Cities_Data\Managed' -HarmonyDirectory 'D:\your-harmony'
```

Build.ps1は製品DLLを構築します。Verify.ps1は各プロジェクトのビルド、破損・欠落・不一致の拒否、保存ロールバック、各グリッドの添字、実ゲームDLLのIL変換等を検査します。信頼するパッチ計画は書き換えず、再生成結果と比較します。Verify-Reproducible.ps1は個人設定や既存bin/objを含まない二つのソースコピーからビルドします。ゲーム・Harmonyの入力が異なれば同じ出力を保証しません。

CIのPublic source checksはChecks、SaveChecks、BridgeChecks、ServiceChecksを実行します。ゲームDLLを持たないCIでは、実ゲームやHarmonyの接続検査は実行していません。CI成功だけをゲーム互換性の証明として扱わないでください。

依存関係はロックファイルで固定し、NuGetの公式取得先を明示します。検査用Mono.Cecilの出典: https://www.nuget.org/packages/Mono.Cecil/0.11.6 。依存の更新はロックファイルのレビューと全検査を伴う変更として扱います。

## プロジェクトの役割

| プロジェクト | 役割 |
|---|---|
| Core | 座標・グリッド・保存形式・整合性検査 |
| ExperimentalPatch | レビュー済みIL計画と命令変換 |
| SaveBridge | 保存前スナップショット、旧形式への投影、復元、ロールバック |
| Utilities | 電気・水道・暖房・サービス資源の拡張と保存 |
| Runtime | ゲーム入口、言語UI、互換性検査、パッチ適用 |
| Checks / SaveChecks / BridgeChecks / ServiceChecks | 純粋ロジックとモックによる検査 |
| PatchChecks / UtilityChecks / RuntimeChecks | ゲームDLLに対する命令・署名・保存形式検査 |
| IntegrationGame | ユーザーの保存・設定を分離した実ゲーム検査 |
| Audit / AuditFixture / PatchBuilder / MetadataFixture / Diagnostics | 静的監査と開発用診断。ゲーム用配布には同梱しません。 |

## 実ゲーム検証の追加依存

IntegrationGameは開発専用です。ゲームとHarmonyに加えて、別途導入したUnityDoorstopのwinhttp.dllが必要です。Prepare.ps1のDoorstopLibraryとHarmonyModDirectory引数で明示します。MOD併用試験には各MODの確認済み版と翻訳等の付属ファイルも必要です。これらをコードリポジトリへ再配布しません。

```powershell
./IntegrationGame/Prepare.ps1 -GameRoot 'D:\your-game' -HarmonyModDirectory 'D:\your-installed-harmony-mod' -HarmonyDirectory 'D:\your-harmony' -DoorstopLibrary 'D:\your-doorstop\winhttp.dll'
./IntegrationGame/Run.ps1 -GameRoot 'D:\your-game' -Mode BaseEntryCreate
./IntegrationGame/Run.ps1 -GameRoot 'D:\your-game' -Mode BaseEntryReload
```

ユーザーのゲームが実行中なら検証を開始しません。検査後のintegration.log、unity.logと入力セーブのハッシュを確認してください。BaseEntryは基本構成の検証で、追加MODや大都市の再現試験ではありません。

## 未確認事項と試験公開の基準

- 人口・建物・道路・車両が多い実都市での長時間運転と、複数世代の保存・再ロード。
- MODが多数ある任意の構成。照合済み版でも、パッチ対象外の動作や設定の組み合わせによる干渉はあり得ます。
- 81 Tilesの旧セーブ全体の移行。fakeGAMの所有区画は読みますが、旧MOD独自の電気・水道等の追加データの完全移行は保証していません。
- 旧81 Tiles形式のユーザー都市の再読み込み成功。保存形式フィクスチャの合格とは分けます。
- 長時間の自然なタクシー走行。現在の乗降試験では到着コールバックを制御しています。
- 前回の併用試験では終了時にUnifiedUIとKwyttoのUI例外があり、無例外の試験とは報告していません。

正式な安定版とするには、上記の実都市コピー試験、保存の複数世代保持、都市オブジェクトの欠落確認、第三者のクリーンビルドとコードレビューが必要です。期限や通過していない検査を根拠に安全を宣言しません。個別のエラー修正と検証範囲はRelease-1.0.ja.mdに記録します。
