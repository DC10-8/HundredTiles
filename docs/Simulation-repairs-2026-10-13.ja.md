# シミュレーション遅延の調査（2026-10-13）

ユーザーの録画とログでは、人口約800人の都市でシミュレーションが遅く、約80秒の間に地区計算の例外が1,300回、建物更新の例外が100回以上抑止されていました。例外の反復は負荷の原因候補ですが、修正による速度改善量は別の測定が必要です。

## 確認できた不具合

- RoadUtilityPolesのZoneBlockPoleFootprintPatch.Prefixに、旧Monoで実装されていないMethodInfo.op_Equalityの呼び出しが1か所ありました。null比較をILのceqへ置き換えます。接触判定や電柱の配置機能は維持します。
- RealismLiteのLastConstructState配列は49,152件固定でした。建物IDがその範囲を超えると建物更新が配列範囲外で停止します。ushortの建物ID全体を保持できる65,536件へ拡張します。資材消費の計算は変更しません。
- More Network Capacity RenewはTM:PEのRemoveNodeFromSimulationを名前だけで検索し、複数のオーバーロードがある確認済み版でAmbiguousMatchExceptionを起こしました。引数型がushortの1引数メソッドへ限定します。

またRealWorldMotionSpeedがカレンダーの1フレームを0.036秒から0.01667秒へ切り替えたログもあります。表示時刻の進みと実際のシミュレーション処理速度は、区別して確認する必要があります。

## 対象ハッシュと局所修正

| DLL | 入力SHA-256 | 修正後SHA-256 |
|---|---|---|
| RoadUtilityPoles | E84ED6D3900EB948DFF0B1D8E9E16153DDE712764E42D9FC8D7EA88165030907 | 54CD43552683DADD35893158AE14BC07A29F8A0FB4D55F7E04110B2D11FD2D55 |
| RealismLite | 4D70FA1E6684E4095F4E6944AC3003F7BBAE6DDEF123BDE72991F96A86D91217 | 2CCD18CDBCDEE06313CB2F68102AAA01D008D015F785696162961F26294BEFEA |
| Renew原版 | C4334AD302F232644E147EC6FC2397C0C328EF26A02BCA8E1CE71A5F0183338B | 83E318E1ED01D4C000EC268A31767EF295A2357814C025DAD6A8E8E631D5C832 |

旧Renew互換パッチ版33977CE2…からの更新では、先に保存した原版バックアップを入力として再生成します。現在のDLLもバックアップします。未知のバージョンを自動的に書き換えることはありません。

ゲームを終了し、固定SDKのあるソースフォルダーで実行します。

```powershell
./NetworkRenewPatcher/Apply-SimulationRepair.ps1 -Dll '<RoadUtilityPoles.dllのパス>'
./NetworkRenewPatcher/Apply-SimulationRepair.ps1 -Dll '<RealismLite.dllのパス>'
./NetworkRenewPatcher/Apply.ps1 -ModDirectory '<3818403598のフォルダー>' -HarmonyDll '<CitiesHarmony.Harmony.dllのパス>'
```

元のDLLはゲーム外のLocalApplicationData/HundredTiles/CompatibilityBackupsへ保存されます。ゲーム終了後にそのファイルを元のパスへコピーすれば復元できます。Steamの更新で修正が戻る場合があります。第三者DLLはHundredTilesの配布ZIPに含めません。

実ゲームの検証結果はValidation-2026-10-13.ja.mdに追記します。ユーザーの都市での速度改善、任意のMOD構成、大都市の長期運用をこの修正だけで保証しません。
