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
            var ctor = FindAncestorOfType<BaseMethodDeclarationSyntax>(initialToken.Parent);



            context.RegisterCodeFix(
                CodeAction.Create(title, c=> ChangeBlock(context.Document, ctor, c, context.Diagnostics), equivalenceKey: title),
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

        private async Task<Document> ChangeBlock(Document document, BaseMethodDeclarationSyntax originalCtor, CancellationToken c,
            IEnumerable<Diagnostic> diagnostics)
        {
            var root = await GetRootWithNormalizedConstructor(document, originalCtor).ConfigureAwait(false);


            if (diagnostics.Any(d => d.Id == MefImportExceptionAnalyzerAnalyzer.DiagnosticId))
            {
                root = AddNamespaceIfMissing(root, SYSTEM_NAMESPACE);
            }

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

        private static async Task<CompilationUnitSyntax> GetRootWithNormalizedConstructor(Document document, BaseMethodDeclarationSyntax originalCtor)
        {
            var tree = await document.GetSyntaxTreeAsync().ConfigureAwait(false);
            CompilationUnitSyntax root = await tree.GetRootAsync() as CompilationUnitSyntax;

            var annotation = new SyntaxAnnotation();

            root = root.ReplaceNode(originalCtor, originalCtor.WithAdditionalAnnotations(annotation));
            var ctorWithAnnotation = root.GetAnnotatedNodes(annotation).Single() as BaseMethodDeclarationSyntax;

            BaseMethodDeclarationSyntax newCtor = ctorWithAnnotation is ConstructorDeclarationSyntax ? 
                CreateConstructorWithTryCatch(ctorWithAnnotation as ConstructorDeclarationSyntax)
                : CreateConstructorWithTryCatch(ctorWithAnnotation as MethodDeclarationSyntax);


            root = root.ReplaceNode(ctorWithAnnotation, newCtor);
            var entirelyNormalizedRoot = root.NormalizeWhitespace();
            BaseMethodDeclarationSyntax ctorInEntirelyNormalized = entirelyNormalizedRoot.GetAnnotatedNodes(annotation).Single() as BaseMethodDeclarationSyntax;

            var ctorInOrig2 = root.GetAnnotatedNodes(annotation).Single() as BaseMethodDeclarationSyntax;

            var newRoot = root.ReplaceNode(ctorInOrig2, ctorInEntirelyNormalized);

            return newRoot;
        }
        
        private static BaseMethodDeclarationSyntax CreateConstructorWithTryCatch(ConstructorDeclarationSyntax originalCtor)
        {
            var originalBlock = originalCtor.Body;

            var newCtor = originalCtor.WithBody(ConstructBlockWithTryCatch(originalBlock)).NormalizeWhitespace();


            return newCtor;
        }

        private static BaseMethodDeclarationSyntax CreateConstructorWithTryCatch(MethodDeclarationSyntax method)
        {
            var originalBlock = method.Body;
            return method.WithBody(ConstructBlockWithTryCatch(originalBlock)).NormalizeWhitespace();
        }

        private static BlockSyntax ConstructBlockWithTryCatch(BlockSyntax originalBlock)
        {
            return Block(

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
                                                                    MemberAccessExpression(
                                                        SyntaxKind.SimpleMemberAccessExpression,
                                                        IdentifierName("e"),
                                                        IdentifierName("Message"))),
                                                                Token(SyntaxKind.CommaToken),
                                                                Argument(
                                                                    IdentifierName("e"))})))))))))
                                    .WithBlock(originalBlock));
        }
    }
}