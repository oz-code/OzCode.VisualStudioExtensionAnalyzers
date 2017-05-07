using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Rename;
using Microsoft.CodeAnalysis.Text;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace MefImportExceptionAnalyzer
{
    [ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(MefImportExceptionAnalyzerCodeFixProvider)), Shared]
    public class MefImportExceptionAnalyzerCodeFixProvider : CodeFixProvider
    {
        private const string title = "Add try.. catch inside";

        private const string ERROR_NOTIFICATION_NAMESPACE = "DebuggerShared.Services.ErrorNotification";
        private const string SYSTEM_NAMESPACE = "System";

        public sealed override ImmutableArray<string> FixableDiagnosticIds
        {
            get { return ImmutableArray.Create(MefImportExceptionAnalyzerAnalyzer.DiagnosticId); }
        }

        public sealed override FixAllProvider GetFixAllProvider()
        {
            return WellKnownFixAllProviders.BatchFixer;
        }

        public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);

            var diagnostic = context.Diagnostics.First();
            var diagnosticSpan = diagnostic.Location.SourceSpan;

            var initialToken = root.FindToken(diagnosticSpan.Start);
            var ctor = FindAncestorOfType<ConstructorDeclarationSyntax>(initialToken.Parent);

            context.RegisterCodeFix(
                CodeAction.Create(title, c=> ChangeBlock(context.Document, ctor, c), equivalenceKey: title),
                diagnostic);
        }

        private T FindAncestorOfType<T>(SyntaxNode node) where T : SyntaxNode
        {
            if (node == null)
                return null;
            if (node is T)
                return node as T;
            return FindAncestorOfType<T>(node.Parent);
        }

        private async Task<Document> ChangeBlock(Document document, ConstructorDeclarationSyntax originalCtor, CancellationToken c)
        {
            ConstructorDeclarationSyntax newCtor = CreateConstructorWithTryCatch(originalCtor);
            var root = await GetRootWithNormalizedConstructor(document, originalCtor, newCtor).ConfigureAwait(false);
            root = AddNamespaceIfMissing(root, ERROR_NOTIFICATION_NAMESPACE);
            root = AddNamespaceIfMissing(root, SYSTEM_NAMESPACE);

            return document.WithSyntaxRoot(root);
        }

        private CompilationUnitSyntax AddNamespaceIfMissing(CompilationUnitSyntax root, string namespaceIdentifyer)
        {
            var ns = root.DescendantNodesAndSelf()
                .OfType<UsingDirectiveSyntax>()
                .FirstOrDefault(elem => elem.Name.ToString() == namespaceIdentifyer);
            if (ns != null)
                return root;

            var usingDirective = SyntaxFactory.UsingDirective(SyntaxFactory.IdentifierName(namespaceIdentifyer))
                .WithTrailingTrivia(SyntaxFactory.EndOfLine("\r\n"));

            var lastUsing = root.DescendantNodesAndSelf().OfType<UsingDirectiveSyntax>().Last();
            root = root.InsertNodesAfter(lastUsing, new[] { usingDirective });

            return root;
        }

        private static async Task<CompilationUnitSyntax> GetRootWithNormalizedConstructor(Document document, ConstructorDeclarationSyntax originalCtor, ConstructorDeclarationSyntax newCtor)
        {
            var tree = await document.GetSyntaxTreeAsync().ConfigureAwait(false);
            CompilationUnitSyntax root = await tree.GetRootAsync() as CompilationUnitSyntax;

            root = root.ReplaceNode(originalCtor, newCtor);
            var entirelyNormalizedRoot = root.NormalizeWhitespace();
            ConstructorDeclarationSyntax ctorInEntirelyNormalized = FindSpecificConstructor(originalCtor.ParameterList, originalCtor.Identifier.Text, entirelyNormalizedRoot);

            var ctorInOrig2 = FindSpecificConstructor(originalCtor.ParameterList, originalCtor.Identifier.Text, root);

            ctorInEntirelyNormalized = ctorInEntirelyNormalized.WithParameterList(originalCtor.ParameterList);
            ctorInEntirelyNormalized = ctorInEntirelyNormalized.WithAttributeLists(originalCtor.AttributeLists);

            var newRoot = root.ReplaceNode(ctorInOrig2, ctorInEntirelyNormalized);

            return newRoot;
        }

        private static ConstructorDeclarationSyntax FindSpecificConstructor(ParameterListSyntax paramList, string identifierText, CompilationUnitSyntax parentNode)
        {
            var res = parentNode.DescendantNodes().
                OfType<ConstructorDeclarationSyntax>().SingleOrDefault(c => c.Identifier.Text == identifierText
                        && IsParamListEqual(c.ParameterList, paramList)
                        && !c.Modifiers.Any(x => x.IsKind(SyntaxKind.StaticKeyword)));

            if (res == null)

                Debugger.Break();


            return res;
        }

        private static bool IsParamListEqual(ParameterListSyntax paramsA, ParameterListSyntax paramsB)

        {
            if (paramsA == null || paramsB == null)
                return false;
            var parametersA = paramsA.Parameters;
            var parametersB = paramsB.Parameters;
            if (parametersA == null || parametersB == null || parametersA.Count != parametersB.Count)
                return false;
            for (int i = 0; i < parametersA.Count; i++)
            {
                var a = Regex.Replace(parametersA[i].ToString(), @"\s+", "");
                var b = Regex.Replace(parametersB[i].ToString(), @"\s+", "");
                if (a != b)
                    return false;
            }
            return true;
        }

        private static ConstructorDeclarationSyntax CreateConstructorWithTryCatch(ConstructorDeclarationSyntax originalCtor)
        {
            var originalBlock = originalCtor.Body;

            var newCtor = originalCtor.WithBody(
                    Block(

                        TryStatement(
                            SingletonList<CatchClauseSyntax>(
                                CatchClause()
                                .WithDeclaration(
                                    CatchDeclaration(
                                        IdentifierName("Exception"))
                                    .WithIdentifier(
                                        Identifier("e")))
                                .WithBlock(
                                    Block(
                                        SingletonList<StatementSyntax>(
                                            ExpressionStatement(
                                                InvocationExpression(
                                                    MemberAccessExpression(
                                                        SyntaxKind.SimpleMemberAccessExpression,
                                                        IdentifierName("ErrorNotificationLogger"),
                                                        IdentifierName("LogErrorWithoutShowingErrorNotificationUI")))
                                                .WithArgumentList(
                                                    ArgumentList(
                                                        SeparatedList<ArgumentSyntax>(
                                                            new SyntaxNodeOrToken[]{
                                                                Argument(
                                                                    LiteralExpression(
                                                                        SyntaxKind.StringLiteralExpression,
                                                                        Literal("Erorr in MEF ctor"))),
                                                                Token(SyntaxKind.CommaToken),
                                                                Argument(
                                                                    IdentifierName("e"))})))))))))
                        .WithBlock(originalBlock))).NormalizeWhitespace();


            return newCtor;
        }

    }
}