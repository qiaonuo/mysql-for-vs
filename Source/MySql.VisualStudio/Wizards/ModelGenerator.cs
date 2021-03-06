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
using System.Linq;
using System.Text;
using MySql.Data.MySqlClient;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio;


namespace MySql.Data.VisualStudio.Wizards
{
  /// <summary>
  /// Abstract model generator of the backend for the template project.
  /// </summary>
  internal abstract class ModelGenerator
  {
    protected MySqlConnection _con;
    protected string _modelName;
    protected string _table;
    protected List<string> _tables;
    protected string _path;
    protected string _artifactNamespace;
    protected List<string> _errors = new List<string>();
    protected List<string> _warnings = new List<string>();
    protected LanguageGenerator Language;
    protected List<String> _tablesIncluded = new List<string>();
    protected IVsOutputWindowPane _generalPane;

    internal List<String> TablesInModel
    {
      get
      {
        return _tablesIncluded;
      }
    }

    internal IEnumerable<string> Errors
    {
      get { return _errors.AsEnumerable(); }
    }

    internal IEnumerable<string> Warnings
    {
      get { return _warnings.AsEnumerable(); } 
    }

    internal ModelGenerator(MySqlConnection con, string modelName, List<string> tables, string path, string artifactNamespace, LanguageGenerator Language)
    {
      if (tables == null)
        throw new ArgumentNullException("tables");

      _con = con;
      _modelName = modelName;
      _tables = tables;
      _path = path;
      _artifactNamespace = artifactNamespace;
      this.Language = Language;

      EnsureGeneralLogInitialized();
    }

    /// <summary>
    /// Generates the model.
    /// </summary>
    /// <returns>Returns the path of the model file generated, or null in case of errors, is so Errors property can be inspected to look for specific errors.</returns>
    internal virtual bool Generate()
    {
      throw new NotImplementedException();
    }

    protected void EnsureGeneralLogInitialized()
    {
      if (_generalPane == null)
      {
        // get the general output window      
        IVsOutputWindow outWindow = Package.GetGlobalService(typeof(SVsOutputWindow)) as IVsOutputWindow;
        Guid generalPaneGuid = VSConstants.GUID_OutWindowGeneralPane;
        if (outWindow != null)
        {
          outWindow.CreatePane(ref generalPaneGuid, "General", 1, 0);
          outWindow.GetPane(ref generalPaneGuid, out _generalPane);
          _generalPane.Activate();
        }
      }
    }
  }
}
