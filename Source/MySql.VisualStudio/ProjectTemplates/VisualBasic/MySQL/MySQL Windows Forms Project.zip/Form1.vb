﻿' ++++++++++++++++++++++++++++++++++++++++++++++++++
' This code is generated by a tool and is provided "as is", without warranty of any kind,
' express or implied, including but not limited to the warranties of merchantability,
' fitness for a particular purpose and non-infringement.
' In no event shall the authors or copyright holders be liable for any claim, damages or
' other liability, whether in an action of contract, tort or otherwise, arising from,
' out of or in connection with the software or the use or other dealings in the software.
' ++++++++++++++++++++++++++++++++++++++++++++++++++
' 

Imports System.ComponentModel
'<WizardGeneratedCode>Namespace_UserCode</WizardGeneratedCode>

Namespace $safeprojectname$

    Public Class Form1

        '<WizardGeneratedCode>Private Variables Frontend</WizardGeneratedCode>

        Private Sub Form1_Load(sender As System.Object, e As System.EventArgs) Handles MyBase.Load
            '<WizardGeneratedCode>Form_Load</WizardGeneratedCode>
        End Sub

        Private Sub ToolStripButton1_Click(sender As System.Object, e As System.EventArgs) Handles ToolStripButton1.Click
            If Not Me.Validate() Then
              Return
            End If
            '<WizardGeneratedCode>Save Event</WizardGeneratedCode>
        End Sub

        Private Sub Form1_FormClosing(sender As System.Object, e As System.Windows.Forms.FormClosingEventArgs) Handles MyBase.FormClosing
            e.Cancel = False
        End Sub

        '<WizardGeneratedCode>Validation Events</WizardGeneratedCode>

        Private Sub bindingNavigatorAddNewItem_Click(sender As System.Object, e As System.EventArgs)
          '<WizardGeneratedCode>Add Event</WizardGeneratedCode>
        End Sub

    End Class

End Namespace