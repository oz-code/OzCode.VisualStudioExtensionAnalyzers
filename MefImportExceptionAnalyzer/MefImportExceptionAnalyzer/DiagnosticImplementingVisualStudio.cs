using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Diagnostics;

namespace MefImportExceptionAnalyzer
{

    class DiagnosticImplementingVisualStudio
    {
        //public const string DiagnosticId = "ImplementVSNamespaceAnalyzer";

        private static readonly LocalizableString Title = "Implementing method without try..catch";
        private static readonly LocalizableString MessageFormat = "Surround with try..catch to report the exception.";
        private static readonly LocalizableString Description = "This method may be called directly by Visual Studio and should be protected with a try..catch in order to properly report the exception to an error monitoring tool.";
        private const string Category = "VSCallback";

        private static DiagnosticDescriptor Rule = new DiagnosticDescriptor(MefImportExceptionAnalyzerAnalyzer.DiagnosticId, Title, MessageFormat, Category, DiagnosticSeverity.Error, isEnabledByDefault: true, description: Description);

        private const string InterfaceStart = "Microsoft.VisualStudio.";
        private SyntaxNodeAnalysisContext _context;

        internal void Analyze(SyntaxNodeAnalysisContext context)
        {
            _context = context;
            var @class = (ClassDeclarationSyntax)context.Node;
            
            CheckDiagNeeded(@class, context.SemanticModel);
        }

        private void CheckDiagNeeded(ClassDeclarationSyntax @class, SemanticModel semanticModel)
        {
            var classSymbol = semanticModel.GetDeclaredSymbol(@class);
            if (classSymbol == null)
                return;

            bool implementsVS = classSymbol.Interfaces.Any(i => i.ToString().StartsWith(InterfaceStart));
            if (!implementsVS)
                return;

            var relevantInterfaces = classSymbol.Interfaces.Where(i => i.ToString().StartsWith(InterfaceStart)).ToList();

            

            var methods = classSymbol.GetMembers().OfType<IMethodSymbol>()
                .Where (m => m.MethodKind.ToString() == "Ordinary")
                .ToList();

            foreach (var methodSymbol in methods)
            {

                bool isWhiteSpaceOnly = DiagnosticCommon.IsWhiteSpaceOnly(FindSyntax(@class, methodSymbol));
                if (isWhiteSpaceOnly)
                    continue;


                bool tryCatchOnAllExists = DiagnosticCommon.IsTryCatchStatementOnly(FindSyntax(@class, methodSymbol));
                if (tryCatchOnAllExists)
                    continue;

                bool implementsInterface = relevantInterfaces
                 .SelectMany(@interface => @interface.GetMembers().OfType<IMethodSymbol>())
                 .Any(method => methodSymbol.Equals(methodSymbol.ContainingType.FindImplementationForInterfaceMember(method)));

                if (implementsInterface)
                {
                    var diag = Diagnostic.Create(Rule, methodSymbol.Locations.First());
                    _context.ReportDiagnostic(diag);
                }
            }
            
        }

        private BaseMethodDeclarationSyntax FindSyntax(ClassDeclarationSyntax @class, IMethodSymbol methodSymbol2)
        {
            var @ref = (methodSymbol2.DeclaringSyntaxReferences.FirstOrDefault());
            if (@ref == null)
                return null;
            return @ref.GetSyntax(new CancellationToken()) as BaseMethodDeclarationSyntax; 
        }
        

        public static bool ImplementsInterface(ITypeSymbol type, string interfaceStr)
        {
            if (type == null)
            {
                return false;
            }

            return type.AllInterfaces.Any(i => i.ToString() == interfaceStr);
        }
    }
}
