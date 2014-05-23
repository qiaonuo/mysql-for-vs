﻿// Copyright © 2008, 2014, Oracle and/or its affiliates. All rights reserved.
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
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Windows.Forms;
using MySql.Data.MySqlClient;
using MySql.Data.VisualStudio.SchemaComparer;
using MySql.Data.VisualStudio.Wizards.WindowsForms;


namespace MySql.Data.VisualStudio.Wizards.WindowsForms
{
  internal class CSharpEntityFrameworkIndividualControlsStrategy : CSharpIndividualControlsStrategy
  {
    internal CSharpEntityFrameworkIndividualControlsStrategy(StrategyConfig config)
      : base(config)
    {
    }

    protected override void WriteUsingUserCode()
    {
      if (DataAccessTech == DataAccessTechnology.EntityFramework5)
      {
        Writer.WriteLine("using System.Data.Objects;");
      }
      else if (DataAccessTech == DataAccessTechnology.EntityFramework6)
      {
        Writer.WriteLine("using System.Data.Entity.Core.Objects;");
      }
    }

    protected override void WriteFormLoadCode()
    {
      Writer.WriteLine("ctx = new Model1Entities();");
      Writer.WriteLine("ObjectResult<{0}> _entities = ctx.{0}.Execute(MergeOption.AppendOnly);", CanonicalTableName);
      Writer.WriteLine("{0}BindingSource.DataSource = _entities;", CanonicalTableName);

      foreach (KeyValuePair<string, Column> kvp in Columns)
      {
        string colName = kvp.Key;
        string idColumnCanonical = GetCanonicalIdentifier(colName);
        Writer.WriteLine("this.{0}TextBox.DataBindings.Add(new System.Windows.Forms.Binding(\"Text\", this.{2}BindingSource, \"{1}\", true ));",
          idColumnCanonical, colName, CanonicalTableName);
      }
    }

    protected override void WriteValidationCode()
    {
      bool validationsEnabled = ValidationsEnabled;
      List<ColumnValidation> validationColumns = ValidationColumns;
      if (validationsEnabled)
      {
        for (int i = 0; i < validationColumns.Count; i++)
        {
          ColumnValidation cv = validationColumns[i];
          string idColumnCanonical = GetCanonicalIdentifier(cv.Column.ColumnName);
          Writer.WriteLine("private void {0}TextBox_Validating(object sender, CancelEventArgs e)", idColumnCanonical);
          Writer.WriteLine("{");
          Writer.WriteLine("  e.Cancel = false;");
          if (cv.Required)
          {
            Writer.WriteLine("  if( string.IsNullOrEmpty( {0}TextBox.Text ) ) {{ ", idColumnCanonical);
            Writer.WriteLine("    e.Cancel = true;");
            Writer.WriteLine("    errorProvider1.SetError( {0}TextBox, \"The field {1} is required\" ); ", idColumnCanonical, cv.Name);
            Writer.WriteLine("  }");
          }
          if (cv.IsNumericType())
          {
            Writer.WriteLine("  int v;");
            Writer.WriteLine("  string s = {0}TextBox.Text;", idColumnCanonical);
            Writer.WriteLine("  if( !int.TryParse( s, out v ) ) {");
            Writer.WriteLine("    e.Cancel = true;");
            Writer.WriteLine("    errorProvider1.SetError( {0}TextBox, \"The field {1} must be numeric.\" );", idColumnCanonical, cv.Name);
            Writer.WriteLine("  }");
            if (cv.MinValue != null)
            {
              Writer.WriteLine(" else if( cv.MinValue > v ) { ");
              Writer.WriteLine("   e.Cancel = true;");
              Writer.WriteLine("   errorProvider1.SetError( {0}TextBox, \"The field {1} must be greater or equal than {2}.\" );", idColumnCanonical, cv.Name, cv.MinValue);
              Writer.WriteLine(" } ");
            }
            if (cv.MaxValue != null)
            {
              Writer.WriteLine(" else if( cv.MaxValue < v ) { ");
              Writer.WriteLine("   e.Cancel = true;");
              Writer.WriteLine("   errorProvider1.SetError( {0}TextBox, \"The field {1} must be lesser or equal than {2}\" );", idColumnCanonical, cv.Name, cv.MaxValue);
              Writer.WriteLine(" } ");
            }
          }
          Writer.WriteLine("  if( !e.Cancel ) {{ errorProvider1.SetError( {0}TextBox, \"\" ); }} ", idColumnCanonical);
          Writer.WriteLine("}");
          Writer.WriteLine();
        }
      }
    }

    protected override void WriteVariablesUserCode()
    {
      Writer.WriteLine("private Model1Entities ctx;");
    }

    protected override void WriteSaveEventCode()
    {
      Writer.WriteLine("{0}BindingSource.EndEdit();", CanonicalTableName);
      Writer.WriteLine("ctx.SaveChanges();");
    }

    protected override void WriteDesignerControlDeclCode()
    {
      Writer.WriteLine("private System.Windows.Forms.BindingSource {0}BindingSource;", CanonicalTableName);
      foreach (KeyValuePair<string, Column> kvp in Columns)
      {
        string idColumnCanonical = GetCanonicalIdentifier(kvp.Key);
        Writer.WriteLine("private System.Windows.Forms.TextBox {0}TextBox;", idColumnCanonical);
        Writer.WriteLine("private System.Windows.Forms.Label {0}Label;", idColumnCanonical);
      }
    }

    protected override void WriteDesignerControlInitCode()
    {
      Writer.WriteLine("this.bindingNavigator1.BindingSource = this.{0}BindingSource;", CanonicalTableName);
      WriteControlInitialization(false);
    }

    protected override void WriteDesignerBeforeSuspendCode()
    {
      Writer.WriteLine("this.{0}BindingSource = new System.Windows.Forms.BindingSource(this.components);", CanonicalTableName);
    }

    protected override void WriteDesignerAfterSuspendCode()
    {
      Writer.WriteLine("((System.ComponentModel.ISupportInitialize)(this.{0}BindingSource)).BeginInit();", CanonicalTableName);
    }

    protected override void WriteBeforeResumeSuspendCode()
    {
      Writer.WriteLine("((System.ComponentModel.ISupportInitialize)(this.{0}BindingSource)).EndInit();", CanonicalTableName);
    }
  }
}