# 起動レビューに基づく修正（2026-10-13）

## 修正内容

- HundredTiles: 自然生成・川修復ボタンは操作名と処理結果を同時に表示します。日英切り替えで両方を更新します。
- CameraSmoothFps: CameraControllerの通常のLateUpdateとFPS BoosterによるFpsBoosterLateUpdateを、引数なしの宣言メソッドとして照合します。ClearFpsBoostが同じ対象で画質を調整している場合は画質の重複変更を止め、カメラ平滑化を残します。
- TrainRidershipBooster: 確認済みTM:PEのCustomPathFindに経路開始、ブースト印、鉄道コスト、徒歩距離の処理を接続します。徒歩距離をTM:PEの既存値より短くしません。実際に徒歩上限を持つオーバーロードだけを書き換えます。
- AlwaysTrainCommute: 公共交通レーンの車種がNone、カテゴリがAllでも、TransportLineAIから鉄道路線を認識します。鉄道限定の経路では列車以外の路線を除外し、通常経路には適用しません。未確認の経路探索では強制モードを無効にします。
- HyperLoad: 親が名前で参照する埋め込み建物を同じCRP内のデータから先に登録します。サイズ・再帰を制限し、別モデルへ置換しません。確認済みNiigata OfficeのLOD ACI欠落は、元の詳細モデルを遠景でも描画することで回避します。このアセットの描画負荷は増えます。
- Network Extensions 2 / TFW: ReflectionTypeLoadException発生時も読み込めた型を前提条件検出に残し、nullの型を除去します。LoaderExceptionsの理由を記録します。欠けた依存DLL自体を修復する処理ではありません。
- Advanced Building Level Control: Building Themesが有効でない場合は任意連携を開始しません。有効時の元の連携処理は保持します。

## 検証範囲

ユーザーのセーブは変更せず、既存の隔離都市を別プロセスで再ロードしました。100タイル、追加領域のサービス資源、Move Itの建物選択・移動、暖房供給、日英表示を確認しました。入力セーブのSHA-256は8F0CE536560AD8279F250D2702A2769257C8C2041E74D4EC7E6F2BB212917935のままです。

埋め込みサブ建物のメタデータを無効にした隔離条件でも、元のJapanese Toiletの登録と親建物の初期化が成功しました。Niigata Officeの元の詳細メッシュも保持されています。元CRPと統合コンテナ内のアパートの部品チェックサムは一致しました。ただし、元の多数のアセットを含む全構成での再検証は別途必要です。

実ゲームMonoでTFWの型読み込み失敗を再現し、339個の読み込めた型が保持されました。ABLCの任意連携も未導入状態で正常に終了しました。カメラの通常名と改名後の対象検索は別プロセスのフィクスチャで確認しています。TM:PEの実際のメソッドへの適用と、列車・バス・地下鉄の路線分類を確認しました。鉄道利用率の長期比較は未実施です。

旧HyperLoadと今回の修正版は、各DLLの完全なSHA-256と既存の意味的メソッドハッシュで照合します。末尾に1バイトを加えた未確認DLLは拒否しました。

問題寸法の非圧縮転送への変更を追加しました。DDSはBC1/BC3仕様に沿ってCPUでRGBAに展開し、PNGは圧縮を避けます。安全な2の累乗寸法は従来の圧縮形式を保持します。旧キャッシュは再利用しない新しいキーを使います。対象画像のGPUメモリ使用量が増えます。

小規模な隔離都市の20秒計測では約60シミュレーションフレーム/秒でした。大規模都市の性能保証ではありません。終了時にUnifiedUIとMove Itの例外が残るため、無例外の起動・終了とは報告しません。大量アセット環境の880件のDirect3D警告と起動時間の改善量も、全構成での測定なしには解消したと判断しません。

修正版による別名保存と別プロセスでの再ロードも成功しました。新しい隔離セーブは93,131,022バイト、SHA-256 E62724837312AE1200B6DB9602CFF66448983B2787EBC9769EA2BE7BDC7F08A1です。読み込み前後でハッシュは不変でした。元の参照セーブ8F0CE536...も保持しました。保存前の未知パッチ検出・除去後の再検証が合格しました。再ロード後20.04秒の計測は1,203シミュレーションフレーム（60.04フレーム/秒）です。終了時の既知UI例外は保存・再ロード両方で残ります。

最新の隔離再ロードログでは、対象アセットのSub building/LOD aciエラー、パッチクラス適用失敗、Direct3D stagingエラーは各0件でした。通常環境に修正版を導入して原版をバックアップし、コピー後のDLLハッシュを検証しました。異なる2つのクリーンソース展開とGitチェックアウトからのビルドは、配布6 DLLと一致しました。

## バイナリ修復ツール

第三者MODの修正版DLLは公開ソースに含めません。自分の導入済み原版から、ハッシュを限定した修復ツールで作ります。ゲームを終了してから実行してください。

```powershell
dotnet build StartupRepairHelper -c Release -p:GameManaged="<ゲームのManagedフォルダ>"
dotnet build NetworkRenewPatcher -c Release
./NetworkRenewPatcher/Apply-StartupRepair.ps1 `
  -ModDll "<原版NetworkExtensions2.dll または AdvancedBuildingLevelControl.dll>" `
  -HelperDll "./StartupRepairHelper/bin/Release/net35/StartupRepairHelper.dll" `
  -GameManaged "<ゲームのManagedフォルダ>"
```

入力版・出力ハッシュが一致しない場合は導入しません。原版はユーザーデータ下のHundredTiles/CompatibilityBackupsへ保存します。Workshop更新で第三者DLLが変わった場合は、自動的に新しい版へ同じ処理を当てません。

## 変更ファイル一覧

HundredTiles公開ソース:

- Runtime/Mod.cs
- Runtime/ReviewedCompatibility.cs
- Runtime/ReviewedCompatibility.tsv
- RuntimeChecks/Program.cs
- IntegrationGame/Bootstrap.cs、Runner.cs、Run.ps1、LiveOptionsChecks.cs、LiveCompatibilityChecks.cs
- IntegrationGame/LiveStartupChecks.cs、LiveTextureDiagnostics.cs（追加）
- NetworkRenewPatcher/Program.cs、StartupRepairs.cs（追加）、Apply-StartupRepair.ps1（追加）
- StartupRepairHelper/StartupRepairHelper.csproj、packages.lock.json、RecoverableTypeScan.cs、OptionalBuildingThemes.cs（追加）
- TextureChecks/TextureChecks.csproj、packages.lock.json、DxtExpansion.cs、TextureFormatStub.cs、Program.cs（追加）
- .github/workflows/source-checks.yml、Export-Source.ps1、README.md、README.ja.md、本書

関連MODのローカルソース:

- CameraSmoothFps: Harmony/FpsBoostPatch.cs、Harmony/HarmonyBootstrap.cs、Source/LoadingExtension.cs、README.md
- TrainRidershipBooster: Utilities/PathFinderTargets.cs（追加）、Utilities/ModCompatibility.cs、Harmony/TransitCostPatch.cs、Harmony/PathFindContextPatch.cs、Harmony/WalkingDistancePatch.cs、Managers/PathFindTuning.cs、README.md
- AlwaysTrainCommute: Core/RailCategoryResolver.cs、Core/PathInspector.cs、Core/RailRouteFilter.cs（追加）、Utilities/PathFinderTargets.cs（追加）、Patches/PathManagerCreatePathPatch.cs、Patches/RailRouteFilterPatch.cs（追加）、README.md
- HyperLoad: Core/Content/ContentLoader.cs、Core/Content/EmbeddedBuildingRecovery.cs（追加）、Core/Content/AssetDecoder.cs、Core/Content/DxtExpansion.cs（追加）、Core/TextureCache/CompressionSafety.cs（追加）、CompressedTextureBuilder.cs、GpuTextureCache.cs、README.md

ソース削除はありません。ゲームDLL、CRP、逆コンパイル資料、個人ログは公開対象に含めません。

BCのブロック・色・アルファの展開は [MicrosoftのBC1仕様](https://learn.microsoft.com/en-us/windows/uwp/graphics-concepts/opaque-and-1-bit-alpha-textures) と [BC3仕様](https://learn.microsoft.com/en-us/windows/uwp/graphics-concepts/textures-with-alpha-channels) を基準にしました。TextureChecksは透明画素、色パレット、2種類のアルファ補間、端の余り、ミップ境界、破損入力の検査です。検査用デコーダーは関連MODで使用するソースの同一コピーです。
