using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Temporary playable investigation surface. It uses the existing status text
/// so the deduction loop can be tested before the final investigation UI exists.
/// </summary>
[DisallowMultipleComponent]
public class CaseInvestigationConsole : MonoBehaviour
{
    [SerializeField] private CaseDirector caseDirector;
    [SerializeField] private CaseInvestigationController investigation;
    [SerializeField] private SurveillanceController surveillance;

    private readonly StringBuilder display = new StringBuilder();
    private string feedback;

    private void Awake()
    {
        if (caseDirector == null)
            caseDirector = GetComponent<CaseDirector>();

        if (investigation == null)
            investigation = GetComponent<CaseInvestigationController>();

        if (surveillance == null)
            surveillance = GetComponent<SurveillanceController>();
    }

    private void LateUpdate()
    {
        if (caseDirector == null || investigation == null ||
            investigation.Phase == InvestigationPhase.Surveillance)
            return;

        HandleInput();
        Render();
    }

    private void HandleInput()
    {
        if (investigation.Phase != InvestigationPhase.Investigation || Keyboard.current == null)
            return;

        if (Keyboard.current.eKey.wasPressedThisFrame)
            ReviewFirstLockedClip();

        if (Keyboard.current.digit1Key.wasPressedThisFrame || Keyboard.current.numpad1Key.wasPressedThisFrame)
            SubmitAccusation("01");

        if (Keyboard.current.digit2Key.wasPressedThisFrame || Keyboard.current.numpad2Key.wasPressedThisFrame)
            SubmitAccusation("02");

        if (Keyboard.current.digit3Key.wasPressedThisFrame || Keyboard.current.numpad3Key.wasPressedThisFrame)
            SubmitAccusation("03");

        if (Keyboard.current.digit4Key.wasPressedThisFrame || Keyboard.current.numpad4Key.wasPressedThisFrame)
            SubmitAccusation("04");
    }

    private void ReviewFirstLockedClip()
    {
        foreach (CaseEvidence evidence in caseDirector.caseDefinition.evidence)
        {
            if (investigation.IsEvidenceUnlocked(evidence.evidenceId))
                continue;

            if (investigation.TryUnlockEvidence(evidence.evidenceId, out string evidenceText))
            {
                feedback = evidenceText;
                return;
            }
        }

        feedback = "No review clips are available.";
    }

    private void SubmitAccusation(string actorId)
    {
        CaseEvidence evidence = FindFirstUnlockedEvidence();

        if (evidence == null)
        {
            feedback = "Review a camera clip before accusing a monkey.";
            return;
        }

        CaseStatement statement = caseDirector.caseDefinition.statements.Find(
            value => value.speakerActorId == actorId
        );

        if (statement == null)
        {
            feedback = "That monkey has no statement to challenge.";
            return;
        }

        investigation.TryAccuse(actorId, statement.statementId, evidence.evidenceId, out feedback);
    }

    private CaseEvidence FindFirstUnlockedEvidence()
    {
        return caseDirector.caseDefinition.evidence.Find(
            evidence => investigation.IsEvidenceUnlocked(evidence.evidenceId)
        );
    }

    private void Render()
    {
        TMP_Text statusText = surveillance == null ? null : surveillance.statusText;

        if (statusText == null)
            return;

        CaseDefinition definition = caseDirector.caseDefinition;
        display.Clear();
        display.AppendLine(definition.caseTitle);
        display.AppendLine($"{definition.crime.itemName} {definition.crime.crimeVerb} from {definition.crime.roomRole}.");
        display.AppendLine();
        display.AppendLine("STATEMENTS");

        foreach (CaseStatement statement in definition.statements)
        {
            display.Append('[')
                .Append(statement.speakerActorId)
                .Append("] ")
                .Append(GetActorName(statement.speakerActorId))
                .Append(": ")
                .AppendLine(statement.text);
        }

        display.AppendLine();

        CaseEvidence unlockedEvidence = FindFirstUnlockedEvidence();

        if (unlockedEvidence == null)
        {
            display.AppendLine($"REVIEW TOKENS: {investigation.RemainingReviewTokens}");
            display.AppendLine("[E] REVIEW A CAMERA CLIP");
        }
        else
        {
            display.AppendLine("REVIEW CLIP");
            display.AppendLine(unlockedEvidence.text);
            display.AppendLine("[1-4] ACCUSE A MONKEY");
        }

        if (!string.IsNullOrWhiteSpace(feedback))
        {
            display.AppendLine();
            display.AppendLine(feedback);
        }

        statusText.text = display.ToString();
    }

    private string GetActorName(string actorId)
    {
        CaseActorSchedule actor = caseDirector.caseDefinition.actorSchedules.Find(
            value => value.actorId == actorId
        );

        return actor == null || string.IsNullOrWhiteSpace(actor.displayName)
            ? "Monkey " + actorId
            : actor.displayName;
    }
}
