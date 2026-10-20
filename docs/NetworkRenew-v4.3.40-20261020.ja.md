# More Network Capacity Renew v4.3.40による起動停止

2026年10月20日の報告では、WorkshopのMore Network Capacity Renewがv4.3.40へ
更新され、HundredTilesの`Unreviewed More Network Capacity Renew wrapper`で停止しました。

| ファイル | 更新後のSHA-256 | 判定 |
| --- | --- | --- |
| MoreNetworkCapacityRenew.dll | `490A4E032B12DE9940AC5F0176FD38A4CB780DDA92AC6733270AF51B802205C8` | 未対応 |
| MoreNetworkCapacity.Core.dll | `067E31563D5B7496E3DEAD4B70B79593E85B405F55BFE2F04BA7ACC2B218B03C` | 検証済みコアと一致 |

新しい起動側DLLには、配列監視、他MODとの連携、交通、保存前のデータ変更や
保存完了処理など多数の変更があります。ファイルハッシュを許可一覧に足すだけの
対応は行っていません。v4.3.40自体の324タイル対応は未完了です。

今回は元のDLLをバックアップし、以下の検証済み組み合わせへ復元しました。

- 起動側: `867BE560EDCFF616E8B737A88CB5F50A51148F866D76F4B038644F611F686A39`
- コア: `067E31563D5B7496E3DEAD4B70B79593E85B405F55BFE2F04BA7ACC2B218B03C`

復元後、両DLLに対する`ReviewedCompatibility.Verify`のメソッド検査は合格しました。
HundredTiles本体のDLL6本も、路盤修正版の配布マニフェストと全て一致しています。
この組み合わせの隔離都市での保存・別プロセス再ロード結果は
[路盤修正の検証記録](Roadbed-SurfacePainter-20261020.ja.md)にあります。
今回の復元後に、ユーザーの実都市を読み直す検証は行っていません。元のセーブは変更していません。

ゲーム終了後に`Restore-ReviewedNetworkCapacity.ps1`で復元できます。指定した復元元の
ハッシュを検証し、変更前DLLを退避してから書き戻します。失敗時は元へ戻します。
Workshopの再更新や整合性確認で新しい起動側DLLへ置き換わった場合は、停止が再発します。
その場合も未知の版の検査を無条件に解除せず、対応版の検証か互換版の復元が必要です。
