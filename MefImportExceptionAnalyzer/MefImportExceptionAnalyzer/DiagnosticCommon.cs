using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MefImportExceptionAnalyzer
{
    static class DiagnosticCommon
    {
        public static bool IsTryCatchStatementOnly(BaseMethodDeclarationSyntax method)
        {
            if (method == null)
                return false;
            var statements = method.Body.Statements;
            return statements.Count == 1
                && statements[0] is TryStatementSyntax;
        }

        public static bool IsWhiteSpaceOnly(BaseMethodDeclarationSyntax method)
        {
            if (method == null)
                return false;
            return method.Body.Statements.Count == 0;
        }
    }
}
