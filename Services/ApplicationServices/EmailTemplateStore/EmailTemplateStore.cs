using System.Collections.Frozen;
using System.Text.RegularExpressions;
using OpenAcademy.Constants;
using Scriban;
using Scriban.Runtime;

namespace OpenAcademy.Services.ApplicationServices.EmailTemplateStore;

public sealed record RenderedEmail(string Subject, string Html);

public sealed class EmailTemplateStore
{
    private static readonly Regex SubjectLine =
        new(@"\A<!--\s*subject:\s*(.*?)\s*-->\s*", RegexOptions.Compiled);

    private sealed record Compiled(Template Subject, Template Body);
    private readonly FrozenDictionary<EEmailTemplate, Compiled> _templates;

    public EmailTemplateStore()
    {
        var asm = typeof(EmailTemplateStore).Assembly;
        var map = new Dictionary<EEmailTemplate, Compiled>();
        foreach (var name in Enum.GetValues<EEmailTemplate>())
        {
            var file = $"{name}.template.html";
            using var stream = asm.GetManifestResourceStream($"templates/{file}")
                ?? throw new InvalidOperationException($"Missing template file {file}");
            using var reader = new StreamReader(stream);
            var text = reader.ReadToEnd();

            var m = SubjectLine.Match(text);
            if (!m.Success)
                throw new InvalidOperationException($"{file} must start with <!-- subject: ... -->");

            var subject = Template.Parse(m.Groups[1].Value, $"{file}#subject");
            var body    = Template.Parse(text[m.Length..], file);
            foreach (var t in new[] { subject, body })
                if (t.HasErrors)
                    throw new InvalidOperationException(string.Join("; ", t.Messages));

            map[name] = new Compiled(subject, body);
        }
        _templates = map.ToFrozenDictionary();
    }

    public RenderedEmail Render(EEmailTemplate name, object model)
    {
        var vars = new ScriptObject();
        vars.Import(model);
        var ctx = new TemplateContext { StrictVariables = false };
        ctx.PushGlobal(vars);

        var c = _templates[name];
        var subject = string.Join(' ',
            c.Subject.Render(ctx).Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)).Trim();
        return new RenderedEmail(subject, c.Body.Render(ctx));
    }
}
