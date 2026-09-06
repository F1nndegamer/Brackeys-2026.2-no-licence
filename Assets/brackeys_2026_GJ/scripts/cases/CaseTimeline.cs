using System;
using System.Collections.Generic;
using UnityEngine;

public enum CaseEventType
{
    CaseStarted,
    BlackoutStarted,
    CrimeCommitted,
    BlackoutEnded,
    ActorEnteredRoom,
    ActorLeftRoom,
    CameraFeedSelected,
    BookmarkAdded,
    InvestigationStarted,
    EvidenceUnlocked,
    AccusationSubmitted,
    CaseResolved
}

[Serializable]
public class CaseTimelineEvent
{
    [SerializeField] private float time;
    [SerializeField] private CaseEventType eventType;
    [SerializeField] private string actorId;
    [SerializeField] private string roomRole;
    [SerializeField] private string relatedRoomRole;

    public float Time => time;
    public CaseEventType EventType => eventType;
    public string ActorId => actorId;
    public string RoomRole => roomRole;
    public string RelatedRoomRole => relatedRoomRole;

    public CaseTimelineEvent(
        float time,
        CaseEventType eventType,
        string actorId = "",
        string roomRole = "",
        string relatedRoomRole = ""
    )
    {
        this.time = time;
        this.eventType = eventType;
        this.actorId = actorId;
        this.roomRole = roomRole;
        this.relatedRoomRole = relatedRoomRole;
    }
}

[Serializable]
public class CameraObservation
{
    [SerializeField] private float startTime;
    [SerializeField] private float endTime;
    [SerializeField] private string roomRole;

    public float StartTime => startTime;
    public float EndTime => endTime;
    public string RoomRole => roomRole;

    public CameraObservation(float startTime, string roomRole)
    {
        this.startTime = startTime;
        endTime = startTime;
        this.roomRole = roomRole;
    }

    public void EndAt(float time)
    {
        endTime = Mathf.Max(startTime, time);
    }
}

public class CaseTimeline : MonoBehaviour
{
    [SerializeField] private List<CaseTimelineEvent> events = new List<CaseTimelineEvent>();
    [SerializeField] private List<CameraObservation> cameraObservations = new List<CameraObservation>();

    private CameraObservation currentCameraObservation;

    public IReadOnlyList<CaseTimelineEvent> Events => events;
    public IReadOnlyList<CameraObservation> CameraObservations => cameraObservations;

    public void ResetTimeline()
    {
        events.Clear();
        cameraObservations.Clear();
        currentCameraObservation = null;
    }

    public void Record(
        float time,
        CaseEventType eventType,
        string actorId = "",
        string roomRole = "",
        string relatedRoomRole = ""
    )
    {
        events.Add(new CaseTimelineEvent(
            time,
            eventType,
            actorId,
            roomRole,
            relatedRoomRole
        ));
    }

    public void BeginCameraObservation(float time, string roomRole)
    {
        EndCameraObservation(time);
        currentCameraObservation = new CameraObservation(time, roomRole);
        cameraObservations.Add(currentCameraObservation);
    }

    public void EndCameraObservation(float time)
    {
        if (currentCameraObservation == null)
            return;

        currentCameraObservation.EndAt(time);
        currentCameraObservation = null;
    }
}
