﻿// Copyright © 2015, 2017, Oracle and/or its affiliates. All rights reserved.
//
// MySQL for Visual Studio is licensed under the terms of the GPLv2
// <http://www.gnu.org/licenses/old-licenses/gpl-2.0.html>, like most
// MySQL Connectors. There are special exceptions to the terms and
// conditions of the GPLv2 as it is applied to this software, see the
// FLOSS License Exception
// <http://www.mysql.com/about/legal/licensing/foss-exception.html>.
//
// This program is free software; you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published
// by the Free Software Foundation; version 2 of the License.
//
// This program is distributed in the hope that it will be useful, but
// WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY
// or FITNESS FOR A PARTICULAR PURPOSE. See the GNU General Public License
// for more details.
//
// You should have received a copy of the GNU General Public License along
// with this program; if not, write to the Free Software Foundation, Inc.,
// 51 Franklin St, Fifth Floor, Boston, MA 02110-1301  USA

using System;
using System.IO;
using System.Windows.Forms;
using Microsoft.VisualStudio.Shell;
using MySql.Data.MySqlClient;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using MySql.Utility.Classes;
using System.Text;
using ConsoleTables.Core;
using MySql.Data.VisualStudio.Properties;
using MySql.Utility.Classes.MySqlWorkbench;
using MySql.Utility.Classes.MySqlX;
using MySql.Utility.Enums;

namespace MySql.Data.VisualStudio.Editors
{
  /// <summary>
  /// This class will handle the logic for the Script Files Editor.
  /// </summary>
  internal sealed partial class MySqlHybridScriptEditor : BaseEditorControl
  {
    #region Fields

    /// <summary>
    /// Variable to store the value to know if the user wants to execute the statements in batch mode or in console mode
    /// </summary>
    private ExecutionModeOption _executionModeOption = ExecutionModeOption.BatchMode;

    /// <summary>
    /// Variable to store the value to know if the user wants to execute the statements in the same session or not
    /// </summary>
    private SessionOption _sessionOption = SessionOption.UseSameSession;

    /// <summary>
    /// Variable to hold the number of tabs created in the output pane
    /// </summary>
    private int _tabCounter;

    /// <summary>
    /// Variable used to executes the script
    /// </summary>
    private MySqlXProxy _mySqlXProxy;

    #endregion Fields

    /// <summary>
    /// Initializes a new instance of the <see cref="MySqlHybridScriptEditor"/> class.
    /// </summary>
    /// <exception cref="System.Exception">MySql Data Provider is not correctly registered</exception>
    public MySqlHybridScriptEditor()
    {
      InitializeComponent();

      Factory = MySqlClientFactory.Instance;
      if (Factory == null)
      {
        throw new Exception("MySql Data Provider is not correctly registered");
      }

      SetConnection(null, string.Empty);
      IsHybrid = true;
      EditorActionsToolStrip = EditorToolStrip;
      Package = MySqlDataProviderPackage.Instance;
      SetBaseEvents(true);
      ClearResults();
      ScriptLanguageType = ScriptLanguageType.JavaScript;
      SetBaseShellConsoleEditorPromptString();
      ToggleEditors(ExecutionModeOption.BatchMode);
#if !VS_SDK_2010
      Microsoft.VisualStudio.PlatformUI.VSColorTheme.ThemeChanged += VSColorTheme_ThemeChanged;
      SetColors();
    }

    /// <summary>
    /// Responds to the event when Visual Studio theme changed.
    /// </summary>
    /// <param name="e">The <see cref="ThemeChangedEventArgs"/> instance containing the event data.</param>
    void VSColorTheme_ThemeChanged(Microsoft.VisualStudio.PlatformUI.ThemeChangedEventArgs e)
    {
      SetColors();
    }

    /// <summary>
    /// Sets the colors corresponding to current Visual Studio theme.
    /// </summary>
    private void SetColors()
    {
      Controls.SetColors();
      BackColor = Utils.BackgroundColor;
      BaseShellConsoleEditor.Controls.SetColors();
      BaseShellConsoleEditor.BackColor = Utils.EditorBackgroundColor;
      BaseShellConsoleEditor.PromptColor = Utils.FontColor;
      BaseShellConsoleEditor.ForeColor = Utils.FontColor;
#endif
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="MySqlHybridScriptEditor"/> class.
    /// </summary>
    /// <param name="sp">The service provider.</param>
    /// <param name="pane">The pane.</param>
    /// <param name="scriptType">Indicates the script type.</param>
    internal MySqlHybridScriptEditor(ServiceProvider sp, MySqlHybridScriptEditorPane pane, ScriptLanguageType scriptType = ScriptLanguageType.JavaScript)
      : this()
    {
      ScriptLanguageType = scriptType;
      ConnectionInfoToolStripDropDownButton.Image = scriptType == ScriptLanguageType.JavaScript
        ? Resources.js_id
        : Resources.py_id;
      SetBaseShellConsoleEditorPromptString();
      Pane = pane;
      ServiceProvider = sp;
      CodeEditor.Init(sp, this);
      if (Package == null)
      {
        return;
      }

      SetConnection(Package.SelectedMySqlConnection, Package.SelectedMySqlConnectionName);
    }

    #region Properties

    /// <summary>
    /// Gets the pane for the current editor. In this case, the pane is from type MySqlScriptEditorPane.
    /// </summary>
    internal MySqlHybridScriptEditorPane Pane { get; set; }

    /// <summary>
    /// The script type.
    /// </summary>
    public ScriptLanguageType ScriptLanguageType { get; set; }

    #endregion Properties

    #region Overrides

    /// <summary>
    /// Gets the file format list.
    /// </summary>
    /// <returns>The string with the file name and extensions for the 'Save as' dialog.</returns>
    protected override string GetFileFormatList()
    {
      switch (ScriptLanguageType)
      {
        case ScriptLanguageType.Sql:
          return "MySQL SQL Files (*.mysql)\n*.mysql\n\n";
        case ScriptLanguageType.JavaScript:
          return "MySQL JavaScript Files (*.myjs)\n*.myjs\n\n";
        case ScriptLanguageType.Python:
          return "MySQL Python Files (*.mypy)\n*.mypy\n\n";
      }

      return string.Empty;
    }

    /// <summary>
    /// Gets the full document path, including file name and extension
    /// </summary>
    /// <returns>The full document path, including file name and extension</returns>
    internal override string GetDocumentPath()
    {
      return Pane.DocumentPath;
    }

    /// <summary>
    /// Sets the document path.
    /// </summary>
    /// <param name="documentPath">New document path.</param>
    internal override void SetDocumentPath(string documentPath)
    {
      Pane.DocumentPath = documentPath;
    }

    /// <summary>
    /// Saves the file.
    /// </summary>
    /// <param name="newFileName">New name of the file.</param>
    protected override void SaveFile(string newFileName)
    {
      using (StreamWriter writer = new StreamWriter(newFileName, false))
      {
        writer.Write(CodeEditor.Text);
      }
    }

    /// <summary>
    /// Loads the file.
    /// </summary>
    /// <param name="newFileName">New name of the file.</param>
    protected override void LoadFile(string newFileName)
    {
      if (!File.Exists(newFileName)) return;
      using (StreamReader reader = new StreamReader(newFileName))
      {
        string sql = reader.ReadToEnd();
        CodeEditor.Text = sql;
      }
    }

    /// <summary>
    /// Gets or sets a value indicating whether this instance is dirty.
    /// </summary>
    protected override bool IsDirty
    {
      get { return CodeEditor.IsDirty; }
      set { CodeEditor.IsDirty = value; }
    }

    #endregion

    /// <summary>
    /// Creates a Tab Page for a ResultSet provided and add it to the tabs result
    /// </summary>
    /// <param name="data">Data to load</param>
    /// <param name="resultNumber">Result counter</param>
    private void CreateResultPane(List<Dictionary<string, object>> data, int resultNumber)
    {
      if (data == null)
      {
        return;
      }

      TabPage newResPage = Utils.CreateResultPage(resultNumber);
      var resultViews = new MySqlHybridScriptResultsetView
      {
        Dock = DockStyle.Fill
      };
      resultViews.LoadData(data);
      newResPage.Controls.Add(resultViews);
      ResultsTabControl.TabPages.Add(newResPage);
    }

    /// <summary>
    /// Executes the script in batch mode.
    /// </summary>
    /// <param name="script">The script.</param>
    private void ExecuteBatchScript(string script)
    {
      var statements = new List<string>();
      switch (ScriptLanguageType)
      {
        case ScriptLanguageType.JavaScript:
          statements = script.BreakIntoJavaScriptStatements();
          break;

        case ScriptLanguageType.Python:
          script = script.NormalizeNewLineCharacters();
          statements = script.BreakIntoPythonStatements();
          break;
      }

      var boxedResults = _mySqlXProxy.ExecuteStatementsBaseAsResultObject(statements.ToArray(), ScriptLanguageType);
      if (!boxedResults.Any())
      {
        return;
      }

      _tabCounter = 1;
      int statementIndex = 0;
      foreach (var boxedResult in boxedResults)
      {
        PrintResult(statements[statementIndex], boxedResult);
        statementIndex++;
      }

      ResultsTabControl.Visible = ResultsTabControl.TabPages.Count > 0;
      CodeEditor.Dock = ResultsTabControl.Visible ? DockStyle.Top : DockStyle.Fill;
      CodeEditor.Focus();
    }

    /// <summary>
    /// Executes the script in console mode.
    /// </summary>
    /// <param name="script">The script.</param>
    private void ExecuteConsoleScript(string script)
    {
      var jsonString = _mySqlXProxy.ExecuteQuery(script, ScriptLanguageType);
      var boxedResult = ExtensionMethods.ToBaseShellResultObject(jsonString);
      PrintResult(script, boxedResult);
      BaseShellConsoleEditor.Focus();
    }

    /// <summary>
    /// Executes the script typed by the user and create a tab page for each statement that returns a resultset from the database otherwise write statement execution info to the output window
    /// </summary>
    /// <param name="script">Script to execute</param>
    private void ExecuteScript(string script)
    {
      if (string.IsNullOrEmpty(script))
      {
        return;
      }

      // Get elapsed time for BaseShell results in case an exception is thrown
      Stopwatch sw = new Stopwatch();
      try
      {
        switch (_sessionOption)
        {
          case SessionOption.UseSameSession:
            if (_mySqlXProxy == null)
            {
              _mySqlXProxy = new MySqlXProxy(Connection.GetXConnectionString(Package.SelectedMySqlConnectionName), true, ScriptLanguageType);
            }

            break;
          case SessionOption.UseNewSession:
            _mySqlXProxy = new MySqlXProxy(Connection.GetXConnectionString(Package.SelectedMySqlConnectionName), false, ScriptLanguageType);
            break;
        }

        sw = Stopwatch.StartNew();
        switch (_executionModeOption)
        {
          case ExecutionModeOption.BatchMode:
            ExecuteBatchScript(script);
            break;

          case ExecutionModeOption.ConsoleMode:
            ExecuteConsoleScript(script);
            break;
        }

        sw.Stop();
      }
      catch (Exception ex)
      {
        sw.Stop();
        WriteToMySqlOutput(script, ex.Message, string.Format("{0} sec", TimeSpan.FromMilliseconds(sw.ElapsedMilliseconds).TotalSeconds), MessageType.Error);
      }
    }

    /// <summary>
    /// Event fired when the Session Option is changed
    /// </summary>
    /// <param name="sender">Sender that calls the event (item clicked)</param>
    /// <param name="e">Event arguments</param>
    private void PreserveVariablesToolStripMenuItem_Click(object sender, EventArgs e)
    {
      var clickedItem = sender as ToolStripMenuItem;
      if (clickedItem == null)
      {
        return;
      }

      _sessionOption = clickedItem.Checked ? SessionOption.UseSameSession : SessionOption.UseNewSession;
      if (_mySqlXProxy != null)
      {
        _mySqlXProxy.CleanConnection();
        _mySqlXProxy = null;
        CodeEditor.Dock = ResultsTabControl.Visible ? DockStyle.Top : DockStyle.Fill;
      }
    }

    /// <summary>
    /// Prints a <see cref="DocResult"/> in the output window showing extended information about the execution result (documents returned, execution time, etc.).
    /// </summary>
    /// <param name="statement">The executed statement.</param>
    /// <param name="docResult">A <see cref="DocResult"/> instance.</param>
    private void PrintDocResult(string statement, DocResult docResult)
    {
      string executionTime = docResult.ExecutionTime;
      var dictionariesList = docResult.Documents;
      PrintGenericResult(statement, dictionariesList, executionTime);
      PrintWarnings(statement, docResult, executionTime);
    }

    /// <summary>
    /// Prints a <see cref="DocResult"/> in the output window showing extended information about the execution result (documents returned, execution time, etc.).
    /// </summary>
    /// <param name="statement">The executed statement.</param>
    /// <param name="dictionariesList">A list of dictionaries of results and information about them.</param>
    /// <param name="executionTime">Execution time formatted to seconds.</param>
    private void PrintGenericResult(string statement, List<Dictionary<string, object>> dictionariesList, string executionTime)
    {
      var count = dictionariesList != null ? dictionariesList.Count : 0;
      switch (_executionModeOption)
      {
        case ExecutionModeOption.BatchMode:
          if (dictionariesList != null && count > 0)
          {
            CreateResultPane(dictionariesList, _tabCounter);
            _tabCounter++;
          }

          break;

        case ExecutionModeOption.ConsoleMode:
          if (dictionariesList == null)
          {
            break;
          }

          foreach (var rowData in dictionariesList)
          {
            var sb = new StringBuilder();
            sb.AppendLine("{");
            int i = 0;
            var rowDataCount = rowData.Count;
            foreach (var kvp in rowData)
            {
              sb.AppendFormat("\t\"{0}\": \"{1}\"{2}{3}", kvp.Key, kvp.Value, i == rowDataCount - 1 ? "" : ",", Environment.NewLine);
              i++;
            }

            sb.AppendLine("},");
            BaseShellConsoleEditor.AddMessage(sb.ToString());
          }

          break;
      }

      WriteToMySqlOutput(statement, string.Format("{0} documents in set.", count), executionTime, MessageType.Information);
    }

    /// <summary>
    /// Prints a boxed result in the output window, showing extended information about the execution result (items affected, execution time, etc.).
    /// </summary>
    /// <param name="statement">The executed statement.</param>
    /// <param name="boxedResult">A boxed execution result.</param>
    private void PrintResult(string statement, object boxedResult)
    {
      if (boxedResult == null)
      {
        return;
      }

      if (boxedResult is Result)
      {
        PrintResult(statement, (Result)boxedResult);
      }
      else if (boxedResult is DocResult)
      {
        PrintDocResult(statement, (DocResult)boxedResult);
      }
      else if (boxedResult is RowResult)
      {
        PrintRowResult(statement, (RowResult)boxedResult);
      }
      else if (boxedResult is SqlResult)
      {
        PrintSqlResult(statement, (SqlResult)boxedResult);
      }
      else
      {
        PrintUnknownResult(statement, boxedResult);
      }
    }

    /// <summary>
    /// Prints a <see cref="Result"/> in the output window showing extended information about the execution result (items affected, execution time, etc.).
    /// </summary>
    /// <param name="statement">The executed statement.</param>
    /// <param name="result">A <see cref="Result"/> instance.</param>
    private void PrintResult(string statement, Result result)
    {
      var resultMessage = new StringBuilder("Query OK");
      long affectedItems = result.AffectedItemCount;
      if (affectedItems >= 0)
      {
        resultMessage.AppendFormat(", {0} item(s) affected", affectedItems);
      }

      var executionTime = result.ExecutionTime;
      WriteToMySqlOutput(statement, resultMessage.ToString(), executionTime, MessageType.Information);
      PrintWarnings(statement, result, executionTime);
    }

    /// <summary>
    /// Prints a <see cref="RowResult"/> in the output window showing extended information about the execution result (rows returned, execution time, etc.).
    /// </summary>
    /// <param name="statement">The executed statement.</param>
    /// <param name="rowResult">A <see cref="RowResult"/> instance.</param>
    private void PrintRowResult(string statement, RowResult rowResult)
    {
      string executionTime = rowResult.ExecutionTime;
      var dictionariesList = rowResult.Data;
      var count = dictionariesList != null ? dictionariesList.Count : 0;

      switch (_executionModeOption)
      {
        case ExecutionModeOption.BatchMode:
          if (dictionariesList != null && count > 0)
          {
            CreateResultPane(dictionariesList, _tabCounter);
            _tabCounter++;
          }

          break;
        case ExecutionModeOption.ConsoleMode:
          if (dictionariesList == null)
          {
            break;
          }

          // Get columns names
          var columns = rowResult.GetColumnNames();

          // Create console table object for output format
          var table = new ConsoleTable(columns.ToArray());
          foreach (var rowData in dictionariesList)
          {
            object[] columnValues = rowData.Select(o => o.Value == null ? (object)"null" : o.Value.ToString()).ToArray();
            var formattedColumnValues = new List<Object>();

            //Format values
            foreach (var value in rowData.Values)
            {
              if (value is Dictionary<string,object>)
              {
                formattedColumnValues.Add(Utils.GetFormattedValue(value));
                continue;
              }

              formattedColumnValues.Add(value);
            }

            table.AddRow(formattedColumnValues.ToArray());
          }

          if (table.Rows.Count > 0)
          {
            BaseShellConsoleEditor.AddMessage(table.ToStringAlternative());
          }
          break;
      }

      var resultMessage = new StringBuilder();

      // If no items are returned, it is a DDL statement (Drop, Create, etc.)
      if (count == 0)
      {
        var sqlResult = rowResult as SqlResult;
        if (sqlResult != null)
        {
          resultMessage.AppendFormat("Query OK, {0} row(s) affected, {1} warning(s)", sqlResult.AffectedRowCount, rowResult.WarningCount);
        }
        else
        {
          resultMessage.AppendFormat("Query OK, {0} warning(s)", rowResult.WarningCount);
        }
      }
      else
      {
        resultMessage.AppendFormat("{0} row(s) in set.", count);
      }

      WriteToMySqlOutput(statement, resultMessage.ToString(), executionTime, MessageType.Information);
      PrintWarnings(statement, rowResult, executionTime);
    }

    /// <summary>
    /// Prints a <see cref="SqlResult"/> in the output window, showing extended information about the execution result (rows returned, execution time, etc.).
    /// </summary>
    /// <param name="statement">The executed statement.</param>
    /// <param name="sqlResult">A <see cref="SqlResult"/> instance.</param>
    private void PrintSqlResult(string statement, SqlResult sqlResult)
    {
      if (sqlResult.HasData)
      {
        PrintRowResult(statement, sqlResult);
      }
      else
      {
        PrintWarnings(statement, sqlResult, sqlResult.ExecutionTime);
      }
    }

    /// <summary>
    /// Prints am unknown boxed result in the output window, showing extended information about the execution result (rows returned, execution time, etc.).
    /// </summary>
    /// <param name="statement">The executed statement.</param>
    /// <param name="boxedResult">A boxed execution result.</param>
    private void PrintUnknownResult(string statement, object boxedResult)
    {
      string executionTime = ExtensionMethods.ZERO_EXECUTION_TIME;
      MessageType messageType = MessageType.Information;

      // TODO: Remove this code when Shell bug 25655034 is fixed.
      var result = ProcessBaseShellBugs(boxedResult.ToString());
      if (result != null)
      {
        WriteToMySqlOutput(statement, result, executionTime, messageType);
        return;
      }

      var dictionariesList = boxedResult.UnknownResultToDictionaryList();
      if (dictionariesList != null)
      {
        // Result is a collection in string format
        PrintGenericResult(statement, dictionariesList, executionTime);
        return;
      }

      // Not a collection, so maybe just an error or informational message.
      var stringResult = boxedResult.ToString();
      if (string.IsNullOrEmpty(stringResult))
      {
        return;
      }

      if (stringResult.Contains("error", StringComparison.InvariantCultureIgnoreCase))
      {
        messageType = MessageType.Error;
      }

      WriteToMySqlOutput(statement, stringResult, executionTime, messageType);
    }

    /// <summary>
    /// Prints the warnings (if exists) of the query execution results, in the output window
    /// </summary>
    /// <param name="script">The executed command.</param>
    /// <param name="result">The BaseResult execution result.</param>
    /// <param name = "duration" > The elapsed time for BaseShell results that doesn't contain the "GetExecutionTime" property.</param>
    private void PrintWarnings(string script, BaseResult result, string duration)
    {
      if (result.WarningCount <= 0)
      {
        return;
      }

      StringBuilder warningsMessages = new StringBuilder();
      warningsMessages.AppendFormat(" Warning Count: {0}\n", result.WarningCount);
      List<Dictionary<String, Object>> warnings = result.Warnings;
      foreach (Dictionary<String, Object> warning in warnings)
      {
        warningsMessages.AppendFormat("{0} ({1}): {2}\n", warning["Level"], warning["Code"], warning["Message"]);
      }

      WriteToMySqlOutput(script, warningsMessages.ToString(), string.Format("{0} / {1}", duration, result.ExecutionTime), MessageType.Warning);
    }

    // TODO: Remove this method when Shell bug 25655034 is fixed.
    /// <summary>
    /// Processes Base Shell result messages to workaround the known bug 25655034.
    /// </summary>
    /// <param name="result">Correct message.</param>
    /// <returns></returns>
    private string ProcessBaseShellBugs(string result)
    {
      if (result.Contains("Result.getLastDocumentId: document id is not available."))
      {
        return "Query Ok";
      }

      return null;
    }

    /// <summary>
    /// Event delegate method fired when the <see cref="RunScriptToolStripButton"/> is clicked.
    /// </summary>
    /// <param name="sender">Sender object.</param>
    /// <param name="e">Event arguments.</param>
    private void RunScriptToolStripButton_Click(object sender, EventArgs e)
    {
      string script = CodeEditor.Text.Trim();
      ClearResults();
      ExecuteScript(script);
      StoreCurrentDatabase();
    }

    /// <summary>
    /// Set the BaseShellConsoleEditor prompt string according the ScriptLanguageType the file format list.
    /// </summary>
    private void SetBaseShellConsoleEditorPromptString()
    {
      switch (ScriptLanguageType)
      {
        case ScriptLanguageType.Sql:
          BaseShellConsoleEditor.PromptString = "mysql-slq>";
          break;
        case ScriptLanguageType.Python:
          BaseShellConsoleEditor.PromptString = "mysql-py>";
          break;
        case ScriptLanguageType.JavaScript:
          BaseShellConsoleEditor.PromptString = "mysql-js>";
          break;
      }
    }

    /// <summary>
    /// Toggles the editors between batch mode and console mode.
    /// </summary>
    /// <param name="executionMode">The execution mode.</param>
    private void ToggleEditors(ExecutionModeOption executionMode)
    {
      try
      {
        panel1.SuspendLayout();
        if (executionMode == ExecutionModeOption.BatchMode)
        {
          panel1.Controls.Remove(BaseShellConsoleEditor);
          panel1.Controls.Add(ResultsTabControl);
          panel1.Controls.Add(splitter1);
          // Register the code editor, to add back its handles and events
          CodeEditor.RegisterEditor();
          panel1.Controls.Add(CodeEditor);
          RunScriptToolStripButton.Enabled = true;
          CodeEditor.Focus();
        }
        else
        {
          panel1.Controls.Remove(ResultsTabControl);
          panel1.Controls.Remove(splitter1);
          // Unregister the code editor, to remove its handles and events
          CodeEditor.UnregisterEditor();
          panel1.Controls.Remove(CodeEditor);
          BaseShellConsoleEditor.Dock = DockStyle.Fill;
          panel1.Controls.Add(BaseShellConsoleEditor);
          RunScriptToolStripButton.Enabled = false;
          BaseShellConsoleEditor.Focus();
        }
      }
      finally
      {
        panel1.ResumeLayout();
      }
    }

    /// <summary>
    /// Event fired when the execution Mode Option is changed
    /// </summary>
    /// <param name="sender">Sender that calls the event (item clicked)</param>
    /// <param name="e">Event arguments</param>
    private void ToolStripMenuItemExecutionMode_ClickHandler(object sender, EventArgs e)
    {
      var clickedItem = sender as ToolStripMenuItem;
      if (clickedItem == null)
      {
        return;
      }

      ExecutionModeToolStripDropDownButton.Text = clickedItem.Text;
      _executionModeOption = (ExecutionModeOption)clickedItem.Tag;
      ToggleEditors(_executionModeOption);
      foreach (ToolStripMenuItem item in ExecutionModeToolStripDropDownButton.DropDownItems)
      {
        item.Checked = item.Name == clickedItem.Name;
      }
    }

    /// <summary>
    /// Writes to the My SQL Output Tool Window.
    /// </summary>
    /// <param name="action">The action.</param>
    /// <param name="message">The message.</param>
    /// <param name="duration">The duration.</param>
    /// <param name="messageType">Type of the message.</param>
    protected override void WriteToMySqlOutput(string action, string message, string duration, MessageType messageType)
    {
      base.WriteToMySqlOutput(action, message, duration, messageType);
      if (_executionModeOption == ExecutionModeOption.ConsoleMode)
      {
        BaseShellConsoleEditor.AddMessage(message);
      }
    }

    /// <summary>
    /// Handles the Command event of the BaseShellConsoleEditor1 control, and execute the command received.
    /// </summary>
    /// <param name="sender">The source of the event.</param>
    /// <param name="e">The <see cref="BaseShellConsoleCommandEventArgs"/> instance containing the event data.</param>
    private void BaseShellConsoleEditor_Command(object sender, BaseShellConsoleCommandEventArgs e)
    {
      if (e.Command == "cls")
      {
        BaseShellConsoleEditor.ClearMessages();
        e.Cancel = true;
        return;
      }

      ExecuteScript(e.Command);
    }
  }
}