using System.Collections.Immutable;

namespace FireChart.Viewer.Core.Reports;

// The check of a component before it is shown: every element is a built-in or a component of the project, with the attributes it takes and the
// children it allows, every expression compiles and names only the variables it can see, and no component includes itself. The renderer trusts a
// component without problems; one with problems is shown as a note
internal static class ReportCheck
{
    // The end of the problem of a call of a component that is not there
    public const string UnknownComponent = "is neither a built-in component nor a component of the project";

    // keys: the keys of locales/strings.resx, every %strings.Key must be one of them; null: the texts are not checked (a package without its own file)
    public static IReadOnlyList<ReportProblem> Check(ComponentDefinition component, Func<string, ComponentDefinition?> components, IReadOnlySet<string>? keys = null)
    {
        if (Forms.SearchFormComponents.IsSearchForm(component.Name))
        {
            return Forms.SearchFormComponents.Check(component, keys);
        }

        var problems = new List<ReportProblem>();
        var checker = new Checker(component, components, problems, keys);
        checker.Run();
        return problems;
    }

    private sealed class Checker(ComponentDefinition component, Func<string, ComponentDefinition?> components, List<ReportProblem> problems, IReadOnlySet<string>? keys)
    {
        public void Run()
        {
            Duplicates(component.Params.Select(param => (param.Name, param.Line)), "parameter");
            Duplicates(component.Data.Select(search => (search.Id, search.Line)), "search");
            Duplicates(component.Slots.Select(slot => (slot, component.Line)), "slot");

            // The searches see the context and the parameters; the body sees the searches too
            var outer = ImmutableHashSet.Create(ReportVariables.Context).Union(component.Params.Select(param => param.Name));
            foreach (var search in component.Data)
            {
                Value(search.Query, outer, search.Line);
                if (search.Query.Source.Split('?')[0] is var type && (type.Length == 0 || !char.IsUpper(type[0])))
                {
                    Add(search.Line, $"the query of {search.Id} starts with a resource type (\"Observation?…\"), not \"{search.Query.Source}\"");
                }
            }

            Nodes(component.Body, outer.Union(component.Data.Select(search => search.Id)), ReportChildren.Any, null, null);
            Cycle(component.Name, []);
        }

        private void Nodes(IEnumerable<ReportNode> nodes, ImmutableHashSet<string> variables, ReportChildren allowed, BuiltInSchema? parent, string? parentName)
        {
            foreach (var node in nodes)
            {
                if (node.Text is { } text)
                {
                    Value(text, variables, node.Line);
                    continue;
                }

                if (allowed == ReportChildren.None)
                {
                    Add(node.Line, $"<{node.Name}> is not allowed here: the parent takes no children");
                    continue;
                }

                if (allowed == ReportChildren.Inline && !ReportSchema.Inline.Contains(node.Name))
                {
                    Add(node.Line, $"<{node.Name}> is not allowed in a line of text; only {string.Join(", ", ReportSchema.Inline)}");
                    continue;
                }

                if (allowed == ReportChildren.Only && parent is not null && !parent.Only.Contains(node.Name))
                {
                    Add(node.Line, $"<{node.Name}> is not allowed in <{parent.Name}>; only {string.Join(", ", parent.Only)}");
                    continue;
                }

                if (ReportSchema.BuiltIns.TryGetValue(node.Name, out var schema))
                {
                    if (schema.Parent is { } only && parentName != only)
                    {
                        Add(node.Line, $"<{node.Name}> stands only in <{only}>");
                        continue;
                    }

                    BuiltIn(node, schema, variables);
                }
                else if (components(node.Name) is { } called)
                {
                    Call(node, called, variables);
                }
                else
                {
                    Add(node.Line, node.Name == ReportSchema.Fill
                                       ? $"<{ReportSchema.Fill}> is only in the call of a component of the project"
                                       : $"<{node.Name}> {UnknownComponent}");
                }
            }
        }

        private void BuiltIn(ReportNode node, BuiltInSchema schema, ImmutableHashSet<string> variables)
        {
            foreach (var (name, value) in node.Attributes)
            {
                if (!schema.Allows(name))
                {
                    Add(node.Line, $"<{node.Name}> has no attribute {name} (it takes {string.Join(", ", schema.Attributes.Keys)})");
                    continue;
                }

                Value(value, variables, node.Line);
            }

            foreach (var required in schema.RequiredAttributes.Where(required => !node.Attributes.ContainsKey(required)))
            {
                Add(node.Line, $"<{node.Name}> needs {required}=\"…\"");
            }

            if (node.Name == ReportSchema.Show && node.Attribute("mode") is { Single: null } mode && !ReportSchema.Modes.Contains(mode.Source))
            {
                Add(node.Line, $"the mode \"{mode.Source}\" is none of {string.Join(", ", ReportSchema.Modes)}");
            }

            if (node.Attribute("tone") is { Single: null } tone && !ReportSchema.Tones.Contains(tone.Source))
            {
                Add(node.Line, $"the tone \"{tone.Source}\" is none of {string.Join(", ", ReportSchema.Tones)}");
            }

            // A Repeat and a Table give their children the item under its name; the other blocks pass the variables on
            var inner = node.Name switch
            {
                ReportSchema.Repeat or ReportSchema.Table => variables.Add(node.Attribute("as")?.Source is { Length: > 0 } itemName ? itemName : ReportVariables.Item),
                ReportSchema.Resolve => variables.Add(node.Attribute("as")?.Source is { Length: > 0 } recordName ? recordName : ReportVariables.Record),
                _ => variables
            };
            Nodes(node.Children, inner, schema.Children, schema, node.Name);
        }

        private void Call(ReportNode node, ComponentDefinition called, ImmutableHashSet<string> variables)
        {
            foreach (var (name, value) in node.Attributes)
            {
                if (called.Params.All(param => param.Name != name))
                {
                    Add(node.Line, $"<{node.Name}> has no parameter {name}");
                    continue;
                }

                Value(value, variables, node.Line);
            }

            foreach (var param in called.Params.Where(param => param.Required && !node.Attributes.ContainsKey(param.Name)))
            {
                Add(node.Line, $"<{node.Name}> needs {param.Name}=\"…\"");
            }

            // The content of a slot is written here, so it sees the variables of this component, not those of the one called
            foreach (var child in node.Children)
            {
                if (child.Name == ReportSchema.Fill)
                {
                    var slot = child.Attribute("slot")?.Source ?? string.Empty;
                    if (!called.Slots.Contains(slot))
                    {
                        Add(child.Line, $"<{node.Name}> has no slot \"{slot}\"");
                    }

                    Nodes(child.Children, variables, ReportChildren.Any, null, null);
                }
                else if (!called.Slots.Contains(string.Empty))
                {
                    Add(child.Line, $"<{node.Name}> takes no content: it has no default <Slot/>");
                }
                else
                {
                    Nodes([child], variables, ReportChildren.Any, null, null);
                }
            }
        }

        private void Value(ReportValue value, ImmutableHashSet<string> variables, int line)
        {
            foreach (var expression in value.Expressions)
            {
                if (ReportExpressions.ErrorOf(expression.Source) is { } error)
                {
                    Add(line, $"{{{{{expression.Source}}}}}: {error}");
                }

                foreach (var unknown in expression.Variables.Where(variable => !variables.Contains(variable) && !IsBuiltInVariable(variable)))
                {
                    Add(line, unknown == "strings"
                        ? $"{{{{{expression.Source}}}}}: a text of the resources stands alone between {{{{ }}}}: {{{{%strings.Key}}}} or {{{{%strings.Key(%a, %b)}}}}"
                        : $"%{unknown} is not known here (known: {string.Join(", ", variables.Order().Select(variable => "%" + variable))})");
                }
            }

            foreach (var text in value.Strings.Where(text => keys is not null && !keys.Contains(text.Key)))
            {
                Add(line, $"%strings.{text.Key} is not a key of locales/strings.resx");
            }
        }

        // The variables of FHIRPath itself (%ucum, %sct, %loinc, %resource, %rootResource)
        private static bool IsBuiltInVariable(string name)
        {
            return name is "ucum" or "sct" or "loinc" or "resource" or "rootResource" or "context";
        }

        // A component that reaches itself through the components it calls would never end
        private void Cycle(string name, ImmutableList<string> path)
        {
            if (path.Contains(name))
            {
                Add(component.Line, $"a cycle of components: {string.Join(" → ", path.Add(name))}");
                return;
            }

            var current = name == component.Name ? component : components(name);
            if (current is null)
            {
                return;
            }

            foreach (var called in Called(current.Body).Distinct())
            {
                Cycle(called, path.Add(name));
            }
        }

        private IEnumerable<string> Called(IEnumerable<ReportNode> nodes)
        {
            foreach (var node in nodes)
            {
                if (node.Name != ReportNode.TextName && !ReportSchema.BuiltIns.ContainsKey(node.Name) && node.Name != ReportSchema.Fill)
                {
                    yield return node.Name;
                }

                foreach (var called in Called(node.Children))
                {
                    yield return called;
                }
            }
        }

        private void Duplicates(IEnumerable<(string Name, int Line)> names, string what)
        {
            foreach (var group in names.GroupBy(name => name.Name).Where(group => group.Count() > 1))
            {
                Add(group.Last().Line, $"the {what} \"{group.Key}\" is declared twice");
            }
        }

        private void Add(int line, string message)
        {
            problems.Add(new ReportProblem(component.Name, line, message));
        }
    }
}
