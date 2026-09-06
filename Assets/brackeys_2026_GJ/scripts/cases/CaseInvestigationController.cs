using System.Collections.Generic;
using UnityEngine;

public enum InvestigationPhase
{
    Surveillance,
    Investigation,
    Resolved
}

[DisallowMultipleComponent]
public class CaseInvestigationController : MonoBehaviour
{
    [SerializeField] private CaseDirector caseDirector;

    private readonly HashSet<string> unlockedEvidenceIds = new HashSet<string>();
    private InvestigationPhase phase = InvestigationPhase.Surveillance;
    private int remainingReviewTokens;

    public InvestigationPhase Phase => phase;
    public int RemainingReviewTokens => remainingReviewTokens;
    public IReadOnlyCollection<string> UnlockedEvidenceIds => unlockedEvidenceIds;

    public bool IsEvidenceUnlocked(string evidenceId)
    {
        return unlockedEvidenceIds.Contains(evidenceId);
    }

    private void Awake()
    {
        if (caseDirector == null)
            caseDirector = GetComponent<CaseDirector>();
    }

    private void OnEnable()
    {
        if (caseDirector != null)
        {
            caseDirector.CaseStarted += HandleCaseStarted;
            caseDirector.CaseEnded += HandleCaseEnded;
        }
    }

    private void OnDisable()
    {
        if (caseDirector != null)
        {
            caseDirector.CaseStarted -= HandleCaseStarted;
            caseDirector.CaseEnded -= HandleCaseEnded;
        }
    }

    public bool TryUnlockEvidence(string evidenceId, out string evidenceText)
    {
        evidenceText = string.Empty;

        if (phase != InvestigationPhase.Investigation || caseDirector == null)
            return false;

        CaseEvidence evidence = FindEvidence(evidenceId);

        if (evidence == null || unlockedEvidenceIds.Contains(evidenceId) ||
            evidence.reviewTokenCost > remainingReviewTokens)
            return false;

        remainingReviewTokens -= evidence.reviewTokenCost;
        unlockedEvidenceIds.Add(evidenceId);
        evidenceText = evidence.text;
        caseDirector.Timeline.Record(caseDirector.CaseTime, CaseEventType.EvidenceUnlocked, roomRole: evidence.roomRole);
        return true;
    }

    public bool TryAccuse(
        string accusedActorId,
        string statementId,
        string evidenceId,
        out string resultText
    )
    {
        resultText = string.Empty;

        if (phase != InvestigationPhase.Investigation || caseDirector == null)
            return false;

        CaseDefinition definition = caseDirector.caseDefinition;
        CaseStatement statement = FindStatement(statementId);
        CaseEvidence evidence = FindEvidence(evidenceId);

        if (statement == null || evidence == null || !unlockedEvidenceIds.Contains(evidenceId))
            return false;

        bool correct =
            accusedActorId == definition.crime.culpritActorId &&
            statement.isCulpritLie &&
            statement.speakerActorId == accusedActorId &&
            evidence.disprovesStatementId == statementId;

        caseDirector.Timeline.Record(
            caseDirector.CaseTime,
            CaseEventType.AccusationSubmitted,
            accusedActorId,
            evidence.roomRole,
            statementId
        );
        phase = InvestigationPhase.Resolved;
        caseDirector.Timeline.Record(
            caseDirector.CaseTime,
            CaseEventType.CaseResolved,
            definition.crime.culpritActorId,
            definition.crime.roomRole
        );

        resultText = correct
            ? $"Correct. {definition.crime.culpritActorId}'s statement breaks the timeline."
            : $"Incorrect. The full timeline will show where the {definition.crime.itemName} went.";
        return correct;
    }

    private void HandleCaseStarted(CaseDefinition definition)
    {
        phase = InvestigationPhase.Surveillance;
        remainingReviewTokens = definition.reviewTokenCount;
        unlockedEvidenceIds.Clear();
    }

    private void HandleCaseEnded(CaseDefinition definition)
    {
        if (phase != InvestigationPhase.Surveillance)
            return;

        phase = InvestigationPhase.Investigation;

        foreach (CaseEvidence evidence in definition.evidence)
        {
            if (evidence.unlockedAtStart)
                unlockedEvidenceIds.Add(evidence.evidenceId);
        }

        caseDirector.Timeline.Record(caseDirector.CaseTime, CaseEventType.InvestigationStarted);
    }

    private CaseStatement FindStatement(string statementId)
    {
        return caseDirector.caseDefinition.statements.Find(
            statement => statement.statementId == statementId
        );
    }

    private CaseEvidence FindEvidence(string evidenceId)
    {
        return caseDirector.caseDefinition.evidence.Find(
            evidence => evidence.evidenceId == evidenceId
        );
    }
}
