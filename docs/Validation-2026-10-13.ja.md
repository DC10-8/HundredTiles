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
