// getspreadseet?d=document&s=seet&a=area

function doGet(e) {
  if (e.parameter.d != null) {
    var spreadsheet = SpreadsheetApp.openById(e.parameter.d);
    if (e.parameter.w == null) { // �ǂݏo��
      if (e.parameter.a != null) { // �Z��
        var value = spreadsheet.getSheetByName(e.parameter.s).getRange(e.parameter.a).getValue();
        Logger.log (value);
        return ContentService.createTextOutput(value).setMimeType(ContentService.MimeType.TEXT);
      } else if (e.parameter.s != null) { // �V�[�g
        var values = spreadsheet.getSheetByName(e.parameter.s).getDataRange().getValues();
        Logger.log(values);
        return ContentService.createTextOutput(JSON.stringify(values)).setMimeType(ContentService.MimeType.TEXT);
      } else if (e.parameter.id != null) { // �V�[�gID�ꗗ
        var sheets = spreadsheet.getSheets();
        var sheetids = [sheets.length];
        for (var i = 0; i < sheets.length; i++) {
          sheetids [i] = sheets [i].getSheetId();
        }
        Logger.log(sheetids);
        return ContentService.createTextOutput(JSON.stringify(sheetids)).setMimeType(ContentService.MimeType.TEXT);
      } else { // �V�[�g���ꗗ
        var sheets = spreadsheet.getSheets();
        var sheetnames = [sheets.length];
        for (var i = 0; i < sheets.length; i++) {
          sheetnames [i] = sheets [i].getName();
        }
        Logger.log(sheetnames);
        return ContentService.createTextOutput(JSON.stringify(sheetnames)).setMimeType(ContentService.MimeType.TEXT);
      }
    } else { // ��������
      if (e.parameter.a != null) { // �Z��
        spreadsheet.getSheetByName(e.parameter.s).getRange(e.parameter.a).setValue(e.parameter.w);
      } else if (e.parameter.s != null) { // �V�[�g
        var sheet = spreadsheet.getSheetByName(e.parameter.s);
        if (sheet != null) {
          sheet.getDataRange().clear();
        } else {
          sheet = spreadsheet.insertSheet(e.parameter.s);
        }
        //return ContentService.createTextOutput(e.parameter.w).setMimeType(ContentService.MimeType.TEXT);
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
    }
  }
  return ContentService.createTextOutput("[]").setMimeType(ContentService.MimeType.TEXT); // ��
}

function doPost (e) {
  return doGet (e);
}
