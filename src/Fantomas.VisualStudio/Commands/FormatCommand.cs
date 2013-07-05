﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Operations;

namespace Hestia.FSharpCommands.Commands
{
    public abstract class FormatCommand : CommandBase
    {
        protected void ExecuteFormat()
        {
            var editorOperations = Services.EditorOperationsFactoryService.GetEditorOperations(TextView);
            using (var textUndoTransaction = TryCreateTextUndoTransaction())
            {
                // Handle the special case of a null ITextUndoTransaction up here because it simplifies
                // the rest of the method.  The implementation of operations such as 
                // AddBeforeTextBufferUndoChangePrimitive will directly access the ITextUndoHistory of 
                // the ITextBuffer.  If there is no history then this operation will throw a NullReferenceException
                // instead of failing gracefully.  If we have an ITextUndoTransaction then we know that the 
                // ITextUndoHistory exists and can call all of the methods as appropriate. 
                if (textUndoTransaction == null)
                {
                    ExecuteFormatCore();
                    return;
                }

                // This command will capture the caret position as it currently exists inside the undo 
                // transaction.  That way the undo command will reset the caret back to this position.  
                editorOperations.AddBeforeTextBufferChangePrimitive();

                if (ExecuteFormatCore())
                {
                    // Capture the caret as it exists now.  This way any redo of this edit will 
                    // reposition the caret as it exists now. 
                    editorOperations.AddAfterTextBufferChangePrimitive();
                    textUndoTransaction.Complete();
                }
                else
                {
                    textUndoTransaction.Cancel();
                }
            }
        }

        private ITextUndoTransaction TryCreateTextUndoTransaction()
        {
            var textBufferUndoManager = Services.TextBufferUndoManagerProvider.GetTextBufferUndoManager(TextBuffer);

            // It is possible for an ITextBuffer to have a null ITextUndoManager.  This will happen in 
            // cases like the differencing viewer.  If VS doesn't consider the document to be editable then 
            // it won't create an undo history for it.  Need to be tolerant of this behavior. 
            if (textBufferUndoManager == null)
            {
                return null;
            }

            return textBufferUndoManager.TextBufferUndoHistory.CreateTransaction("Format Code");
        }

        private bool ExecuteFormatCore()
        {
            string text = TextView.TextSnapshot.GetText();

            ITextBuffer buffer = TextView.TextBuffer;

            IEditorOptions editorOptions = Services.EditorOptionsFactory.GetOptions(buffer);
            int indentSize = editorOptions.GetOptionValue<int>(new IndentSize().Key);
            FantomasOptionsPage customOptions = (FantomasOptionsPage)(Package.GetGlobalService(typeof(FantomasOptionsPage)));

            string source = GetAllText(buffer);

            var isSignatureFile = IsSignatureFile(buffer);

            var config = new Fantomas.FormatConfig.FormatConfig(
                indentSpaceNum: indentSize,
                pageWidth: customOptions.PageWidth,
                semicolonAtEndOfLine: customOptions.SemicolonAtEndOfLine,
                spaceBeforeArgument: customOptions.SpaceBeforeArgument,
                spaceBeforeColon: customOptions.SpaceBeforeColon,
                spaceAfterComma: customOptions.SpaceAfterComma,
                spaceAfterSemicolon: customOptions.SpaceAfterSemicolon,
                indentOnTryWith: customOptions.IndentOnTryWith
                );

            try
            {
                var formatted = GetFormatted(isSignatureFile, source, config);

                using (var edit = buffer.CreateEdit())
                {
                    var setCaretPosition = GetNewCaretPositionSetter();

                    edit.Replace(0, text.Length, formatted);
                    edit.Apply();

                    // TODO: return cursor to the correct position
                    setCaretPosition();

                    return true;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Unable to format.  " + ex.Message);
                return false;
            }
        }

        protected abstract string GetFormatted(bool isSignatureFile, string source, Fantomas.FormatConfig.FormatConfig config);

        protected abstract Action GetNewCaretPositionSetter();

        private static string GetAllText(ITextBuffer buffer)
        {
            string source;

            using (var writer = new StringWriter())
            {
                buffer.CurrentSnapshot.Write(writer);
                writer.Flush();
                source = writer.ToString();
            }
            return source;
        }

        private static bool IsSignatureFile(ITextBuffer buffer)
        {
            ITextDocument document = buffer.Properties.GetProperty<ITextDocument>(typeof(ITextDocument));
            var fileExtension = Path.GetExtension(document.FilePath);
            // There isn't a distinct content type for FSI files, so we have to use the file extension
            var isSignatureFile = ".fsi".Equals(fileExtension, StringComparison.OrdinalIgnoreCase);
            return isSignatureFile;
        }
    }
}
