using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Tcc.Api.Contracts;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ForumTopicRequest([Required] string? Title, [Required] string? Text);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ForumReplyRequest([Required] string? Text);
