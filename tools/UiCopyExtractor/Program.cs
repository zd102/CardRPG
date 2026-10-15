using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Encodings.Web;

var root=Path.GetFullPath(args.FirstOrDefault()??".");
var catalogPath=Path.Combine(root,"game","Data","ui-copy.zh.json");
var catalog=File.Exists(catalogPath)?JsonSerializer.Deserialize<SortedDictionary<string,string>>(File.ReadAllText(catalogPath))!:new();
var count=0;
foreach(var path in Directory.EnumerateFiles(Path.Combine(root,"game","Scripts"),"ChapterGame*.cs"))
{
    var source=File.ReadAllText(path);var tree=CSharpSyntaxTree.ParseText(source);
    var result=new DisplayCopyRewriter(catalog).Visit(tree.GetRoot())!.ToFullString();
    if(source!=result){File.WriteAllText(path,result,new UTF8Encoding(false));count++;}
}
Directory.CreateDirectory(Path.GetDirectoryName(catalogPath)!);
File.WriteAllText(catalogPath,JsonSerializer.Serialize(catalog,new JsonSerializerOptions{WriteIndented=true,Encoder=JavaScriptEncoder.UnsafeRelaxedJsonEscaping})+"\n",new UTF8Encoding(false));
Console.WriteLine($"Extracted {catalog.Count} display templates; changed {count} source files.");

// Only display arguments and display-property assignments are rewritten. Business identifiers,
// comparisons, switch patterns and text used to classify historical events remain untouched.
sealed class DisplayCopyRewriter(SortedDictionary<string,string> catalog):CSharpSyntaxRewriter
{
    public override SyntaxNode? VisitInvocationExpression(InvocationExpressionSyntax node)
    {
        var name=node.Expression is IdentifierNameSyntax id?id.Identifier.ValueText:"";
        var positions=name switch
        {
            "Text" or "Inline"=>new[]{0},"Button" or "NavigationButton"=>new[]{1},
            "Pin"=>new[]{1,4},"Heading"=>new[]{1,2},"Confirm"=>new[]{0,1,3,5},_=>Array.Empty<int>()
        };
        var args=node.ArgumentList.Arguments;
        for(var i=0;i<args.Count;i++)
        {
            var arg=args[i];var expression=positions.Contains(i)?Extract(arg.Expression):(ExpressionSyntax)Visit(arg.Expression)!;
            args=args.Replace(arg,arg.WithExpression(expression));
        }
        return node.WithArgumentList(node.ArgumentList.WithArguments(args));
    }
    public override SyntaxNode? VisitAssignmentExpression(AssignmentExpressionSyntax node)
    {
        var property=node.Left is MemberAccessExpressionSyntax access?access.Name.Identifier.ValueText:node.Left.ToString();
        return property is "Text" or "TooltipText" or "PlaceholderText" or "_detailText" or "_detailTitle" or "_toast"
            ?node.WithRight(Extract(node.Right)):base.VisitAssignmentExpression(node);
    }
    private ExpressionSyntax Extract(ExpressionSyntax expression)
    {
        if(expression is LiteralExpressionSyntax literal && literal.IsKind(SyntaxKind.StringLiteralExpression) && Chinese(literal.Token.ValueText))
            return Call("Text",literal.Token.ValueText,[]).WithTriviaFrom(expression);
        if(expression is InterpolatedStringExpressionSyntax interpolated && Chinese(interpolated.ToString()))
        {
            var template=new StringBuilder();var values=new List<ExpressionSyntax>();
            foreach(var part in interpolated.Contents)
            {
                if(part is InterpolatedStringTextSyntax text)template.Append(text.TextToken.ValueText);
                else if(part is InterpolationSyntax value)
                {
                    template.Append('{').Append(values.Count);
                    if(value.AlignmentClause!=null)template.Append(',').Append(value.AlignmentClause.Value.ToString());
                    if(value.FormatClause!=null)template.Append(':').Append(value.FormatClause.FormatStringToken.ValueText);
                    template.Append('}');values.Add(value.Expression);
                }
            }
            return Call("Format",template.ToString(),values).WithTriviaFrom(expression);
        }
        if(expression is BinaryExpressionSyntax binary && binary.IsKind(SyntaxKind.AddExpression))return binary.WithLeft(Extract(binary.Left)).WithRight(Extract(binary.Right));
        if(expression is ConditionalExpressionSyntax conditional)return conditional.WithWhenTrue(Extract(conditional.WhenTrue)).WithWhenFalse(Extract(conditional.WhenFalse));
        if(expression is ParenthesizedExpressionSyntax parenthesized)return parenthesized.WithExpression(Extract(parenthesized.Expression));
        return (ExpressionSyntax)Visit(expression)!;
    }
    private ExpressionSyntax Call(string method,string template,IEnumerable<ExpressionSyntax> values)
    {
        var key="UI_"+Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(template)))[..12];
        if(catalog.TryGetValue(key,out var existing)&&existing!=template)throw new InvalidOperationException("Display key collision");
        catalog[key]=template;
        var args=new[]{SyntaxFactory.Argument(SyntaxFactory.LiteralExpression(SyntaxKind.StringLiteralExpression,SyntaxFactory.Literal(key)))}.Concat(values.Select(v=>SyntaxFactory.Argument(v.WithoutTrivia())));
        return SyntaxFactory.InvocationExpression(SyntaxFactory.ParseExpression("UiCopy."+method),SyntaxFactory.ArgumentList(SyntaxFactory.SeparatedList(args))).NormalizeWhitespace();
    }
    private static bool Chinese(string value)=>value.Any(c=>c is >= '\u3400' and <= '\u9fff');
}
