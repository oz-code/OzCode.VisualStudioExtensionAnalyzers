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

        private DiagnosticImplementingVisualStudio _diagnosticImplementingVisualStudio;

        private static DiagnosticDescriptor Rule = new DiagnosticDescriptor(DiagnosticId, Title, MessageFormat, Category, DiagnosticSeverity.Error, isEnabledByDefault: true, description: Description);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get { return ImmutableArray.Create(Rule); } }

        public MefImportExceptionAnalyzerAnalyzer()
        {
            _diagnosticImplementingVisualStudio = new DiagnosticImplementingVisualStudio();
        }

        public override void Initialize(AnalysisContext context)
        {
            context.RegisterSyntaxNodeAction(
                AnalyzeConstructor, ImmutableArray.Create(SyntaxKind.ConstructorDeclaration));

            //Methods implementing interface starting with Microsoft.VisualStudio.XXXX
            context.RegisterSyntaxNodeAction(
                _diagnosticImplementingVisualStudio.Analyze, ImmutableArray.Create(SyntaxKind.ClassDeclaration));
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

            bool isWhiteSpaceOnly = DiagnosticCommon.IsWhiteSpaceOnly(ctor);
            if (isWhiteSpaceOnly)
                return false;


            bool tryCatchOnAllExists = DiagnosticCommon.IsTryCatchStatementOnly(ctor);
            if (tryCatchOnAllExists)
                return false;

            return true;
        }

        private static bool IsImportingAttributeExists(BaseMethodDeclarationSyntax ctor)
        {
            var attrs = ctor.AttributeLists.SelectMany(list => list.Attributes);
            return attrs.Any(attr => attr.Name.ToString() == "ImportingConstructor");
        }


    }
}
