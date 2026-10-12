# More Network Capacity Renew 併用

対象はWorkshop ID 3818403598の、次のハッシュのローカルDLLです。バージョン違いを自動的に許可しません。

- Core: `3E40AF8B9FD02C4FB6E96D29FACDFAF06D8D6110FB32A092ACB3D90B7115DDAF`
- Renew原版: `C4334AD302F232644E147EC6FC2397C0C328EF26A02BCA8E1CE71A5F0183338B`
- Renew互換パッチ版: `33977CE2A2C2D83AA14BFB4432244EBEBA2F9ACF29FDB8B9CAD7E53A4A0C5A07`

HundredTilesは電気・水道のノード配列と循環キューを65,024件に対応させ、既存のv1保存形式から移行します。循環キューの順序を保持し、不明な容量・破損・容量縮小は拒否します。サービス資源の保存形式は変更しません。

対象Renew DLLは旧Harmony 1.2 APIを参照しています。現行Cities Harmony環境で読み込みエラーを確認したため、ローカルDLLのAPI参照だけを変換するパッチャーを公開しています。Harmony生成1か所とPatch呼び出し16か所を変更します。対象MODの機能を省略する処理ではありません。元のDLLはゲーム外にバックアップします。第三者DLLはHundredTilesの配布物に含めません。

ゲームを終了し、.NET SDK 10.0.401のある環境で実行します。

```powershell
./NetworkRenewPatcher/Apply.ps1 -ModDirectory '<3818403598のフォルダー>' -HarmonyDll '<CitiesHarmony.Harmony.dllのパス>'
```

Steamの更新・整合性確認で互換パッチが戻る可能性があります。未知の更新版はこのパッチでは処理できません。元に戻すには、ゲーム終了後、スクリプトが表示するバックアップを元のDLLパスへコピーします。

既存都市は別名のコピーと外部バックアップで試してください。容量拡張後の都市から容量MODを外す移行は対応範囲外です。大規模都市、長期運用、任意のMOD構成の安全性を保証するものではありません。実ゲーム検証結果は検証記録に別途記載します。
