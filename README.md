---
title: Googleスプレッドシートを使ったUnityコラボ開発 (多言語テキストと定数表)
tags: Unity C# ScriptableObject gas OAuth
---

# はじめに
- この記事は、[過去に公開した記事](https://qiita.com/tetr4lab/items/4d04e46ac503f19fe1e7)の焼き直しで、大まかに以下の相違があります。
  - `ScriptableObject`と`Addressables`に対応しました。
  - スプレッドシート側の計算式を使わずに、シート全体を取得して解析するようしました。
- 公開当初に比して、以下の点が変更されました。
  - unity 2020.3, 2021.3で確認しました。
  - OAuth 2.0に対応しました。
  - Windowsに依存した可能性が高いです。

## 前提
- unity 6000.0.50f1
    - Addressables 2.5.0
- Googleスプレッドシート
- Googleアカウント
- Windows 11
  - 他のOSではテストされておらず、動作は不明です。
<details><summary>このシステムのセキュリティについて</summary><div>

- このシステムのセキュリティ
    - スプレッドシート
        - 特別な共有設定は不要で、共同編集者のGoogleアカウントに対する共有のみを設定します。
        - ドキュメントIDが、平文でエディターの設定内([EditorPrefs](https://docs.unity3d.com/ja/current/ScriptReference/EditorPrefs.html))に保存されます。
    - Google Apps Script
        - 自分のみが使えるように設定します。
        - 承認したGoogleアカウントの権限で実行され、承認者がアクセス可能な全てのスプレッドシートにアクセス可能です。
        - アプリケーションのURLが、平文でエディターの設定内([EditorPrefs](https://docs.unity3d.com/ja/current/ScriptReference/EditorPrefs.html))に保存されます。
    - Google Cloud Platform
        - クライアント ID、クライアント シークレット、アクセストークン、リフレッシュトークンが、平文でエディターの設定内([EditorPrefs](https://docs.unity3d.com/ja/current/ScriptReference/EditorPrefs.html))に保存されます。

</div></details>

## できること
- 言語別テキストや定数などをGoogleスプレッドシートで共同編集できます。
  - 「シナリオやユーザー向けメッセージなどの編集者」や「各種の初期値を設定する担当者」がプログラマーでない場合に共同制作が容易になります。
  - 多言語対応のテキストと、言語に依存しない固定値 (int、float、string、bool)を扱えます。
- 編集結果は、任意のタイミングでプロジェクトへ取り込むことができます。
- ビルド番号を管理できます。
  - スクリプトから定数として参照できます。
  - ビルド毎に自動更新できます。
  - プラットフォーム毎の番号を統一できます。

## 特徴
- 取り込んだスプレッドシートの情報を、C#スクリプトと言語別ScriptableObjectに変換します。
- ScriptableObjectはAddressablesで管理されます。
- スプレッドシートの取り込みは、別スレッドで実行され、エディターをブロックしません。

# 導入
## クラウドの準備

### Google Apps Script の作成

- [The Apps Script Dashboard](https://script.google.com/home)へアクセスします。
- 「新規スクリプト」を作成します。仮に、`getspreadseet2`と名付けたものとします。
- 「コード.gs」の中身を、"Assets/GetSharedData/Editor/gas~/getspreadseet2.gs"の内容で置き換えます。
  - このファイルは、エディタからは確認できません。
- 「デプロイ」から「新しいデプロイ」を選び、以下を選択します。
    - 次のユーザーとしてアプリケーションを実行: `自分（～）`
    - アクセスできるユーザー: `自分のみ`
- 「デプロイ」ボタンを押します。
  - デプロイする際には、先ず承認が必要です。
- デプロイできたら、《ウェブ アプリケーションの URL》を控えておきます。
  - 後からでも、「デプロイの管理」から確認できます。

### Google Cloud Platform の作成
- Google Cloud Platformの[ダッシュボード](https://console.cloud.google.com/apis/dashboard)へアクセスします。
- 「APIとサービス」→「OAuth 同意画面」で、同意画面を作ります。
    - 初めてなら、まず、プロジェクトを作ることになります。
    - 「テストユーザー」に自分を追加します。
- 「認証情報」に切り替えて、「OAuth クライアント ID」を作成します。
    - 「アプリケーションの種類」は「デスクトップアプリ」にします。
    - 《クライアント ID》と《クライアント シークレット》を控えます。
      - 後からでも、「認証情報」から確認できます。

### スプレッドシートの用意
- [スプレッドシートの雛形](https://drive.google.com/open?id=1NSBz8MvVLGg5l3zdsnGJsB4aOBx9x3kRR48xbmgsjJ4)を開き、ファイルメニューから「コピーを作成…」を選びます。
- フォルダを選んで保存します。
    - Googleドライブに保存されます。
- マイドライブから保存したフォルダへ辿り、コピーされたスプレッドシートを開きます。
- 開いたスプレッドシートのURLで、`https://docs.google.com/spreadsheets/d/～～～/edit`の「/d/～～～/」の部分に注目してください。
    - この「～～～」の部分が、このスプレッドシートの《ドキュメントID》です。
    - このIDは、後で使用しますので、何処か安全な場所に控えておいてください。

## Unityプロジェクトの準備

### アセットの導入

- このリポジトリをベースにするものと想定します。

### 設定
- 「Window」メニューの「GetSharedData > OPen Setting Window」を選択しウインドウを開きます。
- 「Document ID」に、控えておいた《ドキュメントID》を記入します。
- 「Asset Folder」は、プロジェクトのフォルダ構造に合わせて書き換え可能です。
- 「Application URL」に、控えておいた《ウェブ アプリケーションの URL》を記入します。
  - 後からURLを確認するには、[The Apps Script Dashboard](https://script.google.com/home)の「デプロイの管理」を参照します。
- 「OAuth Settings」を開き「Client ID」と「Client Secret」に、控えておいた《クライアント ID》と《クライアント シークレット》を記入します。
  - 後からIDとシークレットを確認するには、[GCPダッシュボード](https://console.cloud.google.com/apis/dashboard)の「認証情報」を参照します。
- 「Build Number」は、ビルド番号を取り込む機構に対する設定です。
  - 「Unified Number」にチェックすると、機種毎のビルド番号を最大のものに合わせます。
  - 「Auto Increment」にチェックすると、ビルド毎に自動的にビルド番号を1増やします。
- 設定はエディターを終了しても保存されます。
    - プロジェクトの設定は、`Player Settings`の`Company Name`と`Product Name`で識別されるプロジェクト毎に保持されます。
    - 「Application URL」は、プロジェクトを横断して保持されます。
    - 全ての設定は、[EditorPrefs](https://docs.unity3d.com/ja/current/ScriptReference/EditorPrefs.html)に平文で保存されます。

# 使い方
## 編集と取り込み

### スプレッドシート
- 「Text」と「Const」は、シート名として予約されています。
    - シートの追加や並び替えは任意に行うことができますが、シート名の重複はできません。
    - 「Text」シートに記載したものは言語切り替えテキストに変換されます。
    - 「Const」シートおよび他のシートに記載したものは定数に変換されます。
      - ただし、シート名が識別子名でない(例えば`#Sheet`のような)シートは無視されます。
- シートでは、行を3つの部分(「列ラベル」、「列ラベルより上」、「列ラベルより下」)に分けます。
    - 「Key」と書かれたセルが最初に見つかった行が、列ラベルになります。
    - 列ラベルより上の行は無視されます。
- 「Key」、「Type」、「Value」、「Comment」、および、[`UnityEngine.SystemLanguage`](https://docs.unity3d.com/ja/current/ScriptReference/SystemLanguage.html)で定義されている名前は、列ラベル名として予約されています。
    - ラベルの文字種や空白文字の有無は**区別されます**。
    - 予約されていない名前の列は無視されます。
- セルの値(計算結果)だけが使われ、シートの計算式や書式は無視されます。
- 「Text」シートでは、「Key」および「Comment」列と、任意の数の言語名の列を記入します。
    - 最も左の列の言語がデフォルトになります。
    - セル内改行は、改行文字`\n`に変換されます。
- 「Const」他の「定数」シートでは、「Key」、「Type」、「Value」、「Comment」列を記入します。
    - 「Const」シートのキーとして、「BuildNumber」、「BundleVersion」が予約されています。

### Unityエディター
- GetSharedDataウィンドウの「GetSharedData」ボタンを押すか、Windowメニューの「GetSharedData > Get Spreadsheet」を選ぶか、そのメニュー項目に表示されるキーボードショートカットを使うことで、スプレッドシートの情報が取り込まれ、コンパイルされます。

### Addressables
- 言語別テキストを格納したアセット`Text_言語名.asset`は、Addressablesに入れてください。
    - アドレスを単純化(Simplify)して、名前だけにしておいてください。
- 再取り込みを行う際は、ファイルは作り直されずに内容だけが上書きされます。
    - Unityエディターで内容を編集することは想定していません。

## スクリプトでの使用
- `using SharedConstant;`を指定してください。

### テキスト
- システムの言語設定(`Application.systemLanguage`)に従って初期設定されます。
- `SystemLanguage Txt.Locale` 言語 (プロパティ)
    - `SystemLanguage`型の値を代入することで、強制的に言語を切り替えます。
    - `SystemLanguage.Unknown`を指定すると、システムの言語設定に従います。
    - 相当する言語がアセットにない場合は、デフォルト(シート最左)の言語が使われます。
    - 切り替えてから実際に値が変化するまでには、アセットをロードする時間分のラグがあります。
- `string Txt.S (Nam.key[, ...])` テキスト (メソッド)
    - キーを指定して現在設定されている言語のテキストを返します。
    - キーを列挙すると該当するテキストを連結して返します。
- `string Txt.F (Nam.key, object arg[, ...])` フォーマットテキスト (メソッド)
    - 指定したキーをフォーマットとして他の引数を展開したテキストを返します。
- `int Txt.N (string str)` キーの逆引き (メソッド)
    - `Txt.S (～)`で得たテキストを渡すと、該当のキーを返します。
    - 未定義なら`-1`を返します。
- キー`Nam.～`は、`const int`として定義された文字列のインデックスです。

### 数値
- キーは`const`として定義されます。
- 「Const」シートのキーは、クラス`Cns`に定義されます。 (`Cns.～`)
  - `Cns.BuildNumber`、`Cns.BundleVersion`は自動的に定義されます。
- 他のシートは、シート名がクラス名になります。 (`《シート名》.～`)

## Unity使用者が複数いる場合の留意点
- 例えば、「Unityを使わない企画者、Unityを使うデザイナとプログラマの3人」といったように、スプレッドシートからUnityに情報を取り込むメンバーが複数いる場合は以下の点に留意してください。
- メンバーで共有する[アセットの設定](#%E3%82%A2%E3%82%BB%E3%83%83%E3%83%88%E3%81%AE%E5%B0%8E%E5%85%A5%E3%81%A8%E8%A8%AD%E5%AE%9A)は、「Project Settings」と「Build/Bundle Number Settings」だけです。
- 「OAuth Settings」は共有してはいけません。つまり、各メンバーそれぞれが[Google Apps Script の作成](#google-apps-script-%E3%81%AE%E4%BD%9C%E6%88%90)を行う必要があります。
- なお、これらの設定は、プロジェクトのフォルダには保存されないので、GitHubなどによるアセットの共有で問題が生じることはありません。

# トラブルシューティング

## 「その操作を実行するには承認が必要です。」

- スクリプトに対する承認が失効しています。
- スクリプトエディタを開いて任意の関数を実行することで承認画面を表示できます。

https://developers.google.com/apps-script/guides/support/troubleshooting?hl=ja#authorization-is

## 期間を開けて使用したら認証でエラーする

- Google Apps Script のデプロイをやり直してみてください。
- デプロイによって、Application URLが更新されるので、インスペクタで新しいURLを設定する必要があります。
