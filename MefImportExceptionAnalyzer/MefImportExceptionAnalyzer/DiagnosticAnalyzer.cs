using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace MefImportExceptionAnalyzer
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class MefImportExceptionAnalyzerAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "MefImportExceptionAnalyzer";

        private static readonly LocalizableString Title = "MEF Import exception Logger";
        private static readonly LocalizableString MessageFormat = "There's a MEF ImportingConstructor without a try..catch block .";
        private static readonly LocalizableString Description = "All MEF ImportingConstructor should have a try..catch on entire content.";
        private const string Category = "MEF";

        private static DiagnosticDescriptor Rule = new DiagnosticDescriptor(DiagnosticId, Title, MessageFormat, Category, DiagnosticSeverity.Error, isEnabledByDefault: true, description: Description);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get { return ImmutableArray.Create(Rule); } }

        public override void Initialize(AnalysisContext context)
        {
            context.RegisterSyntaxNodeAction(AnalyzeConstructor, ImmutableArray.Create(SyntaxKind.ConstructorDeclaration));
        }

        private void AnalyzeConstructor(SyntaxNodeAnalysisContext context)
        {
            var ctor = (ConstructorDeclarationSyntax)context.Node;

            bool isDiagnosticNeeded = IsDiagNeeded(ctor);

            if (isDiagnosticNeeded)
            {
                var diag = Diagnostic.Create(Rule, ctor.GetLocation());
                context.ReportDiagnostic(diag);
            }
        }

        private bool IsDiagNeeded(ConstructorDeclarationSyntax ctor)
        {
            bool isAttributeExists = IsImportingAttributeExists(ctor);
            if (!isAttributeExists)
                return false;

            bool isWhiteSpaceOnly = IsWhiteSpaceOnly(ctor);
            if (isWhiteSpaceOnly)
                return false;


            bool tryCatchOnAllExists = IsTryCatchStatementOnly(ctor);
            if (tryCatchOnAllExists)
                return false;

            return true;
        }


        private static bool IsTryCatchStatementOnly(ConstructorDeclarationSyntax ctor)
        {
            var statements = ctor.Body.Statements;
            return statements.Count == 1
                && statements[0] is TryStatementSyntax;
        }

        private static bool IsWhiteSpaceOnly(ConstructorDeclarationSyntax ctor)
        {
            return ctor.Body.Statements.Count == 0;
        }

        private static bool IsImportingAttributeExists(ConstructorDeclarationSyntax ctor)
        {
            var attrs = ctor.AttributeLists.SelectMany(list => list.Attributes);
            return attrs.Any(attr => attr.Name.ToString() == "ImportingConstructor");
        }


    }
}
