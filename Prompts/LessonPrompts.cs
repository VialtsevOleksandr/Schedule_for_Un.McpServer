using ModelContextProtocol.Server;
using System.Collections.Generic;
using System.ComponentModel;

namespace Schedule_for_Un.McpServer.Prompts;

[McpServerPromptType]
public static class LessonPrompts
{
    [McpServerPrompt, Description(@"
    This request is used immediately after the user requests to delete a lesson and the GetLessonById tool is called to display the user with the details of the found lesson and to request explicit confirmation before proceeding with the deletion.
    Use case:
    1. The client calls the FindLessonByTimeAndGroup tool with the user-provided group name, lesson day and lesson numberOfPair.
    2. The response JSON for the matching lesson is passed into this prompt as the 'lessonJson' argument.
    3. This prompt renders a human-readable summary of the lesson and asks the user to type 'yes' to confirm deletion or 'no' to cancel.
    4. If the user responds with 'yes', the client then invokes the DeleteLesson tool with the same lesson identifier to perform the actual deletion.
    Context:
    • Must follow directly after FindLessonByTimeAndGroup.
    • Should never be used on its own, only in the deletion workflow.
    • Ensures that no accidental deletions occur without explicit user consent.")]
    public static IReadOnlyCollection<string> ConfirmDeleteLesson(
        [Description("JSON-serialized object containing the lesson's details including Id, Subject, Teacher, etc.")] string lessonJson)
    {
        var prompt =
            $"The following lesson has been found:\n{lessonJson}\n\n" +
            "If you truly wish to delete this lesson, please reply with “yes”. " +
            "If you do not want to delete it, reply with “no”.";
        return new List<string> { prompt };
    }
}