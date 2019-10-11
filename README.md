# GetSharedData2
Unity collaboration development using Google Spreadsheet (ScriptableObject, Addressables compatible version)

---
title: Googleスプレッドシートを使ったUnityコラボ開発 (ScriptableObject、Addressables 対応版)
tags: Unity C# ScriptableObject AddressableAssets gas
---
# はじめに
- この記事は、[過去に公開した記事](https://qiita.com/tetr4lab/items/4d04e46ac503f19fe1e7)の焼き直しで、`ScriptableObject`と`Addressables`に対応した版です。
- また、スプレッドシート側の計算式を使わずに、シート全体を取得して解析するように変更しています。

# 前提
- unity 2018.4.8f1
    - Addressables 1.2.4
- Googleスプレッドシート
- Googleアカウント
<details><summary>このシステムのセキュリティについて</summary><div>
- このシステムのセキュリティ
    - スプレッドシート
        - 特別な共有設定は不要で、共同編集者のGoogleアカウントに対する共有のみを設定します。
        - ドキュメントIDが、平文でエディターの設定内([EditorPrefs](https://docs.unity3d.com/ja/current/ScriptReference/EditorPrefs.html))に保存されます。
    - GAS (Google Apps Script)
        - URLを知っていれば誰でも使用できるように設定します。
        - 承認したGoogleアカウントの権限で実行され、承認者のGoogleドライブに保存された全てのスプレッドシートにアクセス可能です。
        - URLが、平文でエディターの設定内([EditorPrefs](https://docs.unity3d.com/ja/current/ScriptReference/EditorPrefs.html))に保存されます。
</div></details>

# できること
- 言語別テキストや定数などをGoogleスプレッドシートで共同編集できます。
- 編集結果は、任意のタイミングでプロジェクトへ取り込むことができます。
- 「シナリオやユーザー向けメッセージなどの編集者」や「各種の初期値を設定する担当者」がプログラマーでない場合に共同制作が容易になります。
- 多言語対応のテキストと、言語に依存しない固定値 (int、float、string、bool)を扱えます。

# 特徴
- 取り込んだスプレッドシートの情報を、C#スクリプトと言語別ScriptableObjectに変換します。
- ScriptableObjectはAddressablesで管理されます。
- スプレッドシートの取り込みは、別スレッドで実行され、エディターをブロックしません。

# 導入

### Google Apps Script の作成

- [The Apps Script Dashboard](https://script.google.com/home)へアクセスします。
- 「新規スクリプト」を作成し、`getspreadseet`と名付けます。
- 「コード.gs」の中身を、以下のコードで置き換えます。

```js:getspreadseet/コード.gs
function doGet(e) {
  if (e.parameter.k == '《Access Key》') {
    var spreadsheet = SpreadsheetApp.openById(e.parameter.d);
    if (e.parameter.w == null) { // 読み出し
      if (e.parameter.a != null) { // セル
        var value = spreadsheet.getSheetByName(e.parameter.s).getRange(e.parameter.a).getValue();
        Logger.log (value);
        return ContentService.createTextOutput(value).setMimeType(ContentService.MimeType.TEXT);
      } else if (e.parameter.s != null) { // シート
        var values = spreadsheet.getSheetByName(e.parameter.s).getDataRange().getValues();
        Logger.log(values);
        return ContentService.createTextOutput(JSON.stringify(values)).setMimeType(ContentService.MimeType.TEXT);
      } else if (e.parameter.id != null) { // シートID一覧
        var sheets = spreadsheet.getSheets();
        var sheetids = [sheets.length];
        for (var i = 0; i < sheets.length; i++) {
          sheetids [i] = sheets [i].getSheetId();
        }
        Logger.log(sheetids);
        return ContentService.createTextOutput(JSON.stringify(sheetids)).setMimeType(ContentService.MimeType.TEXT);
      } else { // シート名一覧
        var sheets = spreadsheet.getSheets();
        var sheetnames = [sheets.length];
        for (var i = 0; i < sheets.length; i++) {
          sheetnames [i] = sheets [i].getName();
        }
        Logger.log(sheetnames);
        return ContentService.createTextOutput(JSON.stringify(sheetnames)).setMimeType(ContentService.MimeType.TEXT);
      }
      return ContentService.createTextOutput("[]").setMimeType(ContentService.MimeType.TEXT); // 空json
    } else { // 書き込み
      if (e.parameter.a != null) { // セル
        spreadsheet.getSheetByName(e.parameter.s).getRange(e.parameter.a).setValue(e.parameter.w);
      } else if (e.parameter.s != null) { // シート
        var sheet = spreadsheet.getSheetByName(e.parameter.s);
        if (sheet != null) {
          sheet.getDataRange().clear();
        } else {
          sheet = spreadsheet.insertSheet(e.parameter.s);
        }
        var values = JSON.parse(e.parameter.w);
        sheet.getRange(1,1,values.length,values[0].length).setValues(values);
        sheet.autoResizeColumns (1, values[0].length);
        if (e.parameter.r != null) {
          sheet.setFrozenRows(e.parameter.r);
        }
        if (e.parameter.c != null) {
          sheet.setFrozenColumns(e.parameter.c);
        }
      }
      return ContentService.createTextOutput("").setMimeType(ContentService.MimeType.TEXT); // 空
    }
  }
}

function doPost (e) {
  return doGet (e);
}
```
- **スクリプト冒頭の「《Access Key》」を、キーワードで書き換えてください。**
    - これは秘密のURLの一部になるので、ランダムで十分長い文字列にしてください。
    - このキーワードは、後で使用しますので、何処か安全な場所に控えておいてください。
- スクリプトを保存します。
- 「公開」メニューから、「ウェブ アプリケーションとして導入」を選び、以下を選択します。
    - プロジェクト バージョン: `New`
    - 次のユーザーとしてアプリケーションを実行: `自分（～）`
    - アプリケーションにアクセスできるユーザー: `全員（匿名ユーザーを含む）`
- 「導入」ボタンを押します。
<details><summary>続いて、スクリプトを「承認」する必要があります。</summary><div>
- 承認の進め方
    - 「承認が必要です」と言われたら「許可を確認」するボタンを押します。
    - 「このアプリは確認されていません」と表示されたら「詳細」をクリックします。
    - 「getspreadsheet（安全ではないページ）に移動」と表示されるので、クリックします。
    - 「getspreadsheet が Google アカウントへのアクセスをリクエストしています」と表示されるので、「許可」します。
    - 「現在のウェブ アプリケーションの URL」が表示されます。
        - このURLは、後で使用しますので、何処か安全な場所に控えておいてください。
    - これによって、URLを知る誰でも、承認したアカウントの権限で、スクリプトが実行可能になります。
        - スクリプトは、アカウントにアクセス権がありドキュメントIDが分かる任意のスプレッドシートを読み取ることができます。
        - スクリプトと権限は、いつでも[The Apps Script Dashboard](https://script.google.com/home)から削除可能です。
</div></details>

# プロジェクトの準備

### スプレッドシートの用意
- [スプレッドシートの雛形](https://drive.google.com/open?id=1NSBz8MvVLGg5l3zdsnGJsB4aOBx9x3kRR48xbmgsjJ4)を開き、ファイルメニューから「コピーを作成…」を選びます。
- フォルダを選んで保存します。
    - Googleドライブに保存されます。(ドライブの容量は消費しません。)
- マイドライブから保存したフォルダへ辿り、コピーされたスプレッドシートを開きます。
- 開いたスプレッドシートのURLで、`https://docs.google.com/spreadsheets/d/～～～/edit#gid=～`の「/d/～～～/edit#」の部分に注目してください。
    - この「～～～」の部分が、このスプレッドシートの`Document ID`です。
    - このIDは、後で使用しますので、何処か安全な場所に控えておいてください。

### Addressablesの導入
- パッケージマネージャーを使ってAddressablesを導入します。
- Window (Menu) > Package Manager > Pakkages (Window) > Addressables > Install (Button)

### アセットの導入と設定

- プロジェクトにアセット「GetSharedData.unitypackage」を導入してください。
- 「Window」メニューの「GetSharedData > OPen Setting Window」を選択しウインドウを開きます。
- 「Application URL*」に、控えておいた「ウェブ アプリケーションの URL」を記入します。
- 「Access Key*」に、控えておいたキーワードを記入します。
- 「Document ID」に、控えておいたスプレッドシートのIDを記入します。
- 「Asset Folder」は、プロジェクトのフォルダ構造に合わせて書き換え可能です。
- 設定はエディターを終了しても保存されます。
    - 「*」の付く設定は、プロジェクトを跨いで共有されます。
    - その他の設定はプロジェクト毎に保持されます。
        - プロジェクトは、`Player Settings`の`Company Name`と`Product Name`で識別されますので、適切に設定してください。
    - 全ての設定は、[EditorPrefs](https://docs.unity3d.com/ja/current/ScriptReference/EditorPrefs.html)に平文で保存されます。

# 使い方

### スプレッドシート
- 「Text」と「Const」は、シート名として予約されています。
    - シートの追加や並び替えは任意に行うことができますが、シート名の重複はできません。
    - 「Text」シートに記載したものは言語切り替えテキスト、「Const」シートに記載したものは定数に変換されます。
    - 他のシートは無視されます。
- シートでは、行を3つの部分(「列ラベル」、「列ラベルより上」、「列ラベルより下」)に分けます。
    - 「Key」と書かれたセルが最初に見つかった行が、列ラベルになります。
    - 列ラベルより上は無視されます。
- 「Key」、「Type」、「Value」、「Comment」、および、[`UnityEngine.SystemLanguage`](https://docs.unity3d.com/ja/current/ScriptReference/SystemLanguage.html)で定義されている名前は、列ラベル名として予約されています。
    - ラベルの文字種や空白文字の有無は**区別されます**。
    - 予約されていない名前の列は無視されます。
- セルの値(計算結果)だけが使われ、シートの計算式や書式は無視されます。
- 「Text」シートでは、「Key」および「Comment」列と、任意の数の言語名の列を記入します。
    - 最も左の列の言語がデフォルトになります。
    - セル内改行は、改行文字`\n`に変換されます。
- 「Const」シートでは、「Key」、「Type」、「Value」、「Comment」列を記入します。
    - キーとして、「BuildNumber」、「BundleVersion」が予約されています。

### Unityエディター
- GetSharedDataウィンドウの「GetSharedData」ボタンを押すか、Windowメニューの「GetSharedData > Get Spreadsheet」を選ぶか、そのメニュー項目に表示されるキーボードショートカットを使うことで、スプレッドシートの情報が取り込まれ、コンパイルされます。

### Addressables
- 言語別テキストを格納したアセット`Text_言語名.asset`は、Addressablesに入れてください。
    - アドレスを単純化(Simplify)して、名前だけにしておいてください。
- 再取り込みを行う際は、ファイルは作り直されずに内容だけが上書きされます。
    - Unityエディターで内容を編集することは想定していません。

### スクリプトでの使用
- `using SharedConstant;`を指定してください。

#### テキスト
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

#### 数値
- キーは`const`として定義されます。
- `Cns.～`として使用します。
- `Cns.BuildNumber`、`Cns.BundleVersion`が自動的に定義されます。

## Unity使用者が複数いる場合の留意点
- 例えば、「Unityを使わない企画者、Unityを使うデザイナとプログラマの3人」といったように、スプレッドシートからUnityに情報を取り込むメンバーが複数いる場合は以下の点に留意してください。
- メンバーで共有する[アセットの設定](#%E3%82%A2%E3%82%BB%E3%83%83%E3%83%88%E3%81%AE%E5%B0%8E%E5%85%A5%E3%81%A8%E8%A8%AD%E5%AE%9A)は、「Document ID」と「Assets/Scripts/ Folder」だけです。
- 「Application URL*」と「Access Key*」は共有してはいけません。つまり、各メンバーそれぞれが[Google Apps Script の作成](#google-apps-script-%E3%81%AE%E4%BD%9C%E6%88%90)を行う必要があります。
- なお、これらの設定は、プロジェクトのフォルダには保存されないので、GitHubなどによるアセットの共有で問題が生じることはありません。
