using ModelContextProtocol.Server;
using System.Collections.Generic;
using System.ComponentModel;

namespace Schedule_for_Un.McpServer.Prompts;

[McpServerPromptType]
public static class TeacherPrompts
{
    [McpServerPrompt, Description(
    @"This request is used immediately after the user requests to delete a teacher and the GetTeachersByName tool is called to display the user with the details of the found teacher and to request explicit confirmation before proceeding with the deletion.
    Use case:
    1. The client calls the GetTeachersByName tool with the user-provided teacher name.
    2. The response JSON for the matching teacher is passed into this prompt as the 'teacherJson' argument.
    3. This prompt renders a human-readable summary of the teacher and asks the user to type 'yes' to confirm deletion or 'no' to cancel.
    4. If the user responds with 'yes', the client then invokes the DeleteTeacher tool with the same teacher identifier to perform the actual deletion.
    Context:
    • Must follow directly after GetTeachersByName.
    • Should never be used on its own, only in the deletion workflow.
    • Ensures that no accidental deletions occur without explicit user consent.")]    
    public static IReadOnlyCollection<string> ConfirmDeleteTeacher(
        [Description("JSON-serialized object containing the teacher's Id, Name, Surname")] string teacherJson)
    {
        var prompt =
            $"The following teacher has been found:\n{teacherJson}\n\n" +
            "If you truly wish to delete this teacher, please reply with “yes”. " +
            "If you do not want to delete it, reply with “no”.";
        return new List<string> { prompt };
    }
   
}