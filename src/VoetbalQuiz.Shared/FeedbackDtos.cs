namespace VoetbalQuiz.Shared;

public enum FeedbackType { Bug, Feature }

public enum FeedbackStatus { Nieuw, Bekeken, Gepland, Opgelost, Afgewezen }

public record FeedbackRequest(FeedbackType Type, string Title, string Description);

public record FeedbackItem(
    int Id, FeedbackType Type, string Title, string Description, string UserName,
    DateTime CreatedAt, FeedbackStatus Status, string? Reply, bool IsMine);

public record FeedbackUpdate(FeedbackStatus Status, string? Reply);

public record FeedbackOverview(bool IsAdmin, List<FeedbackItem> Items);
