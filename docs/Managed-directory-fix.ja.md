# 起動時のManagedディレクトリ初期化修正（2026-10-13）

ユーザーのゲームログで `AssemblyFile.Resolve → SerializerHooks.Verify → SessionGate.Enable` の `Game managed directory was not initialized.` を確認しました。

原因は、起動用途ゲートの追加で `SerializerHooks.Verify` が `Installer.Install` より先に実行されるようになった一方、Managedディレクトリの初期化が `Installer.Install` 内だけに残っていたことです。ゲームDLLがメモリから読み込まれて `Assembly.Location` が空の場合に失敗します。従来の隔離環境ではゲームDLLのLocationが存在したため、この条件を検出できませんでした。

`Installer.InitializeManagedDirectory(Application.dataPath)` をゲート開始時に呼び、都市・エディタ双方で照合前にパスを設定します。都市用インストーラも同じ初期化関数を使用します。ファイル存在、ゲームDLL全体のハッシュ、メモリとディスクのModuleVersionId一致の検査は維持します。

回帰検査ではゲームDLLを `Assembly.Load(byte[])` で読み込み、ManagedDirectoryをnullにして実際のSerializerHooks.Verifyの失敗を再現し、初期化後の成功を確認します。実ゲートのILでも初期化呼び出しが照合呼び出しより先にあることを検査します。Verify.ps1に組み込みました。隔離ゲームのプラグイン入口検査も、ManagedDirectoryを空にした状態から開始し、実ゲームのdataPath/Managedが設定されたことを検査します。

修正は初期化順序のみです。セーブ形式、起動用途の操作方法、互換性の対象版は変更しません。

確認結果:
- メモリ読み込みの回帰検査: エラー再現、初期化後の照合成功、ゲートの呼び出し順序検査が合格。
- 実プラグイン入口の未初期化状態から、エディタ用途の起動・Asset Importer画面への到達と通常サイズの配列を確認。
- 同じく都市用途で485対象のパッチ適用と既存の隔離都市コピーの再ロードが合格。入力セーブのSHA-256は `D2B5F399B170F1DB764E784E57907FBDC5CD0ABCF54C8E1A37CC8FCDAE49D766` のまま。
- 正式なVerify.ps1（回帰検査含む）が合格。Releaseビルド0警告・0エラー。二つのクリーンソースの6DLLが一致し、配布DLLと一致。
- 以前からのUnifiedUI・Move Itの終了時例外、および別MOD構成でのエディタ初期化例外は今回の修正対象ではない。
