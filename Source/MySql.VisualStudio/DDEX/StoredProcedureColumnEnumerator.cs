// Copyright � 2008, 2016, Oracle and/or its affiliates. All rights reserved.
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
// 51 Franklin St, Fifth Floor, Boston, MA 02110-1301  USA.

using System;
using Microsoft.VisualStudio.Data;
using System.Data.Common;
using Microsoft.VisualStudio.Data.AdoDotNet;
using System.Data;

namespace MySql.Data.VisualStudio.DDEX
{
  class StoredProcedureColumnEnumerator : DataObjectEnumerator
  {
    public override DataReader EnumerateObjects(string typeName, object[] items,
        object[] restrictions, string sort, object[] parameters)
    {
      DbConnection conn = (DbConnection)Connection.GetLockedProviderObject();
      try
      {
        string spName = String.Format("{0}.{1}", restrictions[1], restrictions[2]);
        DataTable schemaDataTable;

        if (conn.State != ConnectionState.Open)
          conn.Open();

        DbCommand cmd = conn.CreateCommand();
        cmd.CommandType = CommandType.StoredProcedure;
        SetCommandParameters(conn, cmd, restrictions);

        if (cmd.DoesStoredProcedureContainsSignals(restrictions))
        {
          schemaDataTable = cmd.GetSchemaDataTableFromStoredProcedureDefinition(restrictions);
        }

        else
        {
          schemaDataTable = cmd.GetSchemaDataTableFromStoredProcedureExecution(spName, restrictions);
        }

        if (schemaDataTable == null)
        {
          schemaDataTable = new DataTable();
        }

        return new AdoDotNetDataTableReader(schemaDataTable);
      }
      finally
      {
        Connection.UnlockProviderObject();
      }
    }

    private object GetDefaultValue(string dataType)
    {
      if (dataType == "VARCHAR" || dataType == "VARBINARY" ||
          dataType == "ENUM" || dataType == "SET" || dataType == "CHAR")
        return "";

      return 0;
    }

    /// <summary>
    /// This method set the parameters to the DbCommand object, which will be used by the stored procedure.
    /// The parameters are pulled from the "restrictions" object.
    /// </summary>
    /// <param name="conn">
    /// The DbConnection created previously in the parent method "EnumerateObjects"
    /// </param>
    /// <param name="cmd">
    /// The DbCommand created previously in the parent method "EnumerateObjects"
    /// </param>
    /// <param name="restrictions">
    /// An array of objects containing information about the database name, the schema and the sProc name (among other information).
    /// </param>
    private void SetCommandParameters(DbConnection conn, DbCommand cmd, object[] restrictions)
    {
      string[] parmRest = new string[4];
      parmRest[0] = (string)restrictions[0];
      parmRest[1] = (string)restrictions[1];
      parmRest[2] = (string)restrictions[2];
      parmRest[3] = (string)restrictions[3];
      DataTable parmTable = conn.GetSchema("Procedure Parameters", parmRest);

      foreach (DataRow row in parmTable.Rows)
      {
        if (row["ORDINAL_POSITION"].Equals(0)) continue;

        DbParameter p = cmd.CreateParameter();
        p.ParameterName = row["PARAMETER_NAME"].ToString();
        p.Value = GetDefaultValue(row["DATA_TYPE"].ToString());
        switch (row["PARAMETER_MODE"].ToString())
        {
          case "IN":
            p.Direction = ParameterDirection.Input;
            break;
          case "OUT":
            p.Direction = ParameterDirection.Output;
            break;
          case "INOUT":
            p.Direction = ParameterDirection.InputOutput;
            break;
        }

        cmd.Parameters.Add(p);
      }
    }
  }
}
