using System;
using System.Windows.Forms;
using System.Data;
using System.Data.Common;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell.Interop;

namespace MySql.VSTools
{
    internal class ProcedureNode : ExplorerNode
    {
        private string body;
        private string schema;

        public ProcedureNode(ExplorerNode parent, string caption, DataRow row)
            : base(parent, caption)
        {
            schema = row["ROUTINE_SCHEMA"].ToString();
            body = row["ROUTINE_DEFINITION"].ToString();
        }

        public ProcedureNode(ExplorerNode parent, string caption, string body)
            : base(parent, caption)
        {
            this.body = body;
            schema = GetDatabaseNode().Caption;
            ItemId = VSConstants.VSITEMID_NIL;
        }


        public override uint MenuId
        {
            get { return PkgCmdIDList.ProcedureCtxtMenu; }
        }

        public override uint IconIndex
        {
            get { return 4; }
        }

        public override bool Expandable
        {
            get { return false; }
        }

        public override void Populate()
        {
        }

        public override void DoCommand(int commandId)
        {
            switch (commandId)
            {
                case PkgCmdIDList.cmdidDelete:
                    Delete();
                    break;
                case PkgCmdIDList.cmdidOpen:
                    Open();
                    break;
                default:
                    base.DoCommand(commandId);
                    break;
            }
        }

        private void Delete()
        {
            // first make sure the user is sure
            if (MessageBox.Show(
                String.Format(MyVSTools.GetResourceString("DeleteConfirm"),
                Caption),
                MyVSTools.GetResourceString("DeleteConfirmTitle"),
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question) == DialogResult.No)
                return;

            string sql = String.Format("DROP PROCEDURE {0}.{1}", schema, Caption);
            try
            {
                ExecuteNonQuery(sql);
                //delete was successful, remove this node
                Parent.RemoveChild(this);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, 
                    String.Format(MyVSTools.GetResourceString("UnableToDeleteTitle"),
                    Caption), MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        internal void Open()
        {
            StoredProcedureEditor editor = new StoredProcedureEditor(
                Caption, GetDatabaseNode().Caption, body, GetOpenConnection());
            OpenEditor(editor);
        }
    }
}