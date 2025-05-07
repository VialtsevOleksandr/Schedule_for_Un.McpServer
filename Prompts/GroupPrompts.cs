using ModelContextProtocol.Server;
using System.Collections.Generic;
using System.ComponentModel;

namespace Schedule_for_Un.McpServer.Prompts;

[McpServerPromptType]
public static class GroupPrompts
{
    [McpServerPrompt, Description(
    @"This request is used immediately after the user requests to delete a group and the GetGroupsByName tool is called to display the user with the details of the found group and to request explicit confirmation before proceeding with the deletion.
    Use case:
    1. The client calls the GetGroupsByName tool with the user-provided group name.
    2. The response JSON for the matching group is passed into this prompt as the 'groupJson' argument.
    3. This prompt renders a human-readable summary of the group and asks the user to type 'yes' to confirm deletion or 'no' to cancel.
    4. If the user responds with 'yes', the client then invokes the DeleteGroup tool with the same group identifier to perform the actual deletion.
    Context:
    • Must follow directly after GetGroupsByName.
    • Should never be used on its own, only in the deletion workflow.
    • Ensures that no accidental deletions occur without explicit user consent.")]    
    public static IReadOnlyCollection<string> ConfirmDeleteGroup(
        [Description("JSON-serialized object containing the group's Id, Name, Course, Specialty")] string groupJson)
    {
        var prompt =
            $"The following group has been found:\n{groupJson}\n\n" +
            "If you truly wish to delete this group, please reply with “yes”. " +
            "If you do not want to delete it, reply with “no”.";
        return new List<string> { prompt };
    }

    
}
