# Unity Image Process Tool

Unity 6 + URP 向けの、ノードベース画像処理ツールです。  
`ImageProcessGraphAsset` を編集し、`Texture -> Shader -> Output` の流れで画像処理を組み立てます。

## 概要

- エディタ拡張として `Tools/sugi.cc/Image Process Tool` から起動できます
- `Parameter` / `Shader` / `Output` ノードを接続して処理フローを作成できます
- `Shader` ノードは、設定した `Shader` / `ShaderGraph` のプロパティから入力ポートを自動生成できます
- グラフ編集時に自動実行され、各ノードのプレビューを確認できます
- 出力は `RenderTexture` への反映、または PNG / EXR / Texture2D Asset として保存できます

## 動作環境

- Unity 6.3 LTS (`6000.3`)
- URP (`com.unity.render-pipelines.universal` 17.3.0)
- Shader Graph (`com.unity.shadergraph` 17.3.0)

## インストール

### Package Manager から Git URL で導入する

1. Unity で `Window > Package Manager` を開きます
2. 左上の `+` ボタンを押します
3. `Add package from git URL...` を選びます
4. 次の文字列をそのままコピーして入力し、`Add` を押します

```text
https://github.com/sugi-cho/UnityImageProcessTool.git?path=/Packages/cc.sugi.imageprocesstool
```

## フォルダ構成

- `Packages/cc.sugi.imageprocesstool`
  - 本体パッケージ
- `Assets/ImageProcessToolSample`
  - サンプルのグラフ、`ShaderGraph`、テクスチャ、シーン

## 使い方

1. Unity でプロジェクトを開きます
2. `Tools/sugi.cc/Image Process Tool` を開きます
3. `New` で `ImageProcessGraphAsset` を作成します
4. `Add Parameter`、`Add Shader`、`Add Output` でノードを追加します
5. `Shader` ノードに `Shader` または `ShaderGraph` を設定します
6. `Sync Shader Ports` を押して、シェーダープロパティから入力ポートを生成します
7. ノード同士を接続します
8. `Validate` で構成を確認します
9. 必要に応じて出力を保存、または `ImageProcessGraphRunner` から `RenderTexture` に書き出します

## ShaderGraph の制限

このツールは内部で `Material` を生成し、`Graphics.Blit(...)` で実行しています。  
そのため、使える `ShaderGraph` には次の前提があります。

- `Material` として生成できること
- `Graphics.Blit` で描画可能なパスを持つこと
- 入出力をシェーダープロパティ経由で扱えること
- 出力は 1 枚のテクスチャのみ

## 現在の実装範囲

- 実行確認済みの基本構成は `Parameter -> Shader -> Output`
- `Shader` ノードの入力型は `Texture` / `Float` / `Vector4` / `Color`
- 必須入力の未接続、循環参照、型不一致はバリデーションで検出されます

## 主な制限

- 複数出力には未対応です
- シェーダーの隠しプロパティは自動ポート化されません
- シェーダー固有の特殊な実行文脈は再現しません
- 実行結果の解像度は、最初に見つかった入力テクスチャに依存します
- 入力テクスチャがない場合の出力サイズは `512x512` です

## 補足

実装の中心は `Packages/cc.sugi.imageprocesstool/Runtime/Execution/ImageProcessGraphExecutor.cs` です。  
`ShaderGraph` のポート同期は `Packages/cc.sugi.imageprocesstool/Editor/Utilities/ShaderNodePortSynchronizer.cs` で行っています。
