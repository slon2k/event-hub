namespace EventHub.Domain.UnitTests;

public class EventTests
{
    // ── Helpers ───────────────────────────────────────────────────────────────

    private static Event CreatePublishedEvent(int? capacity = null)
    {
        var ev = Event.Create(
            title: "Team Kickoff",
            description: "Annual kickoff",
            dateTime: DateTimeOffset.UtcNow.AddDays(7),
            location: "Berlin",
            capacity: capacity,
            organizerId: "organizer-oid-001");
        ev.Publish();
        ev.ClearDomainEvents();
        return ev;
    }

    private static (string rawToken, string tokenHash, DateTimeOffset expiresAt) FakeToken() =>
        ("raw-token", "hashed-token", DateTimeOffset.UtcNow.AddHours(72));

    // ── Create ────────────────────────────────────────────────────────────────

    public class Create
    {
        [Fact]
        public void WithValidData_SetsAllProperties()
        {
            var before = DateTimeOffset.UtcNow;
            var futureDate = DateTimeOffset.UtcNow.AddDays(14);

            var ev = Event.Create("Conf 2026", "Description", futureDate, "London", 50, "org-1");

            Assert.Equal("Conf 2026", ev.Title);
            Assert.Equal("Description", ev.Description);
            Assert.Equal(futureDate, ev.DateTime);
            Assert.Equal("London", ev.Location);
            Assert.Equal(50, ev.Capacity);
            Assert.Equal("org-1", ev.OrganizerId);
            Assert.Equal(EventStatus.Draft, ev.Status);
            Assert.NotEqual(Guid.Empty, ev.Id);
            Assert.True(ev.CreatedAt >= before);
            Assert.Empty(ev.Invitations);
        }

        [Fact]
        public void WithPastDateTime_ThrowsDomainException()
        {
            var ex = Assert.Throws<DomainException>(() =>
                Event.Create("Event", null, DateTimeOffset.UtcNow.AddMinutes(-1), null, null, "org-1"));

            Assert.Contains("future", ex.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void WithCurrentDateTime_ThrowsDomainException()
        {
            Assert.Throws<DomainException>(() =>
                Event.Create("Event", null, DateTimeOffset.UtcNow, null, null, "org-1"));
        }

        [Fact]
        public void WithZeroCapacity_ThrowsDomainException()
        {
            var ex = Assert.Throws<DomainException>(() =>
                Event.Create("Event", null, DateTimeOffset.UtcNow.AddDays(1), null, 0, "org-1"));

            Assert.Contains("positive", ex.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void WithNegativeCapacity_ThrowsDomainException()
        {
            Assert.Throws<DomainException>(() =>
                Event.Create("Event", null, DateTimeOffset.UtcNow.AddDays(1), null, -1, "org-1"));
        }

        [Fact]
        public void WithNullCapacity_Succeeds()
        {
            var ev = Event.Create("Event", null, DateTimeOffset.UtcNow.AddDays(1), null, null, "org-1");

            Assert.Null(ev.Capacity);
        }

        [Fact]
        public void WithEmptyTitle_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(() =>
                Event.Create("", null, DateTimeOffset.UtcNow.AddDays(1), null, null, "org-1"));
        }

        [Fact]
        public void WithEmptyOrganizerId_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(() =>
                Event.Create("Title", null, DateTimeOffset.UtcNow.AddDays(1), null, null, ""));
        }

        [Fact]
        public void NoDomainEventsRaised()
        {
            var ev = Event.Create("Event", null, DateTimeOffset.UtcNow.AddDays(1), null, null, "org-1");

            Assert.Empty(ev.DomainEvents);
        }
    }

    // ── Publish ───────────────────────────────────────────────────────────────

    public class Publish
    {
        [Fact]
        public void WhenDraft_ChangesStatusToPublished()
        {
            var ev = Event.Create("Event", null, DateTimeOffset.UtcNow.AddDays(1), null, null, "org-1");

            ev.Publish();

            Assert.Equal(EventStatus.Published, ev.Status);
        }

        [Fact]
        public void WhenAlreadyPublished_ThrowsDomainException()
        {
            var ev = CreatePublishedEvent();

            Assert.Throws<DomainException>(() => ev.Publish());
        }

        [Fact]
        public void WhenCancelled_ThrowsDomainException()
        {
            var ev = CreatePublishedEvent();
            ev.Cancel();

            Assert.Throws<DomainException>(() => ev.Publish());
        }
    }

    // ── Update ────────────────────────────────────────────────────────────────

    public class Update
    {
        [Fact]
        public void WhenDraft_UpdatesAllFields()
        {
            var ev = Event.Create("Old Title", "Old Desc", DateTimeOffset.UtcNow.AddDays(1), "Old", 10, "org-1");
            var newDate = DateTimeOffset.UtcNow.AddDays(30);

            ev.Update("New Title", "New Desc", newDate, "New Location", 20);

            Assert.Equal("New Title", ev.Title);
            Assert.Equal("New Desc", ev.Description);
            Assert.Equal(newDate, ev.DateTime);
            Assert.Equal("New Location", ev.Location);
            Assert.Equal(20, ev.Capacity);
        }

        [Fact]
        public void WhenPublished_UpdatesSuccessfully()
        {
            var ev = CreatePublishedEvent();
            var newDate = DateTimeOffset.UtcNow.AddDays(14);

            ev.Update("New Title", null, newDate, null, null);

            Assert.Equal("New Title", ev.Title);
        }

        [Fact]
        public void WhenCancelled_ThrowsDomainException()
        {
            var ev = CreatePublishedEvent();
            ev.Cancel();

            Assert.Throws<DomainException>(() =>
                ev.Update("Title", null, DateTimeOffset.UtcNow.AddDays(1), null, null));
        }

        [Fact]
        public void WithPastDate_ThrowsDomainException()
        {
            var ev = CreatePublishedEvent();

            Assert.Throws<DomainException>(() =>
                ev.Update("Title", null, DateTimeOffset.UtcNow.AddMinutes(-1), null, null));
        }
    }

    // ── Cancel ────────────────────────────────────────────────────────────────

    public class Cancel
    {
        [Fact]
        public void WhenPublished_ChangesStatusToCancelled()
        {
            var ev = CreatePublishedEvent();

            ev.Cancel();

            Assert.Equal(EventStatus.Cancelled, ev.Status);
        }

        [Fact]
        public void WhenDraft_ChangesStatusToCancelled()
        {
            var ev = Event.Create("Event", null, DateTimeOffset.UtcNow.AddDays(1), null, null, "org-1");

            ev.Cancel();

            Assert.Equal(EventStatus.Cancelled, ev.Status);
        }

        [Fact]
        public void WhenAlreadyCancelled_ThrowsDomainException()
        {
            var ev = CreatePublishedEvent();
            ev.Cancel();
            ev.ClearDomainEvents();

            Assert.Throws<DomainException>(() => ev.Cancel());
        }

        [Fact]
        public void RaisesEventCancelledDomainEvent()
        {
            var ev = CreatePublishedEvent();

            ev.Cancel();

            var domainEvent = Assert.Single(ev.DomainEvents);
            Assert.IsType<EventCancelled>(domainEvent);
        }

        [Fact]
        public void WithPendingAndAcceptedInvitations_IncludesEmailsInDomainEvent()
        {
            var ev = CreatePublishedEvent();
            var (rawToken, tokenHash, expiresAt) = FakeToken();

            ev.AddInvitation("alice@example.com", rawToken, tokenHash, expiresAt);
            ev.AddInvitation("bob@example.com", rawToken + "2", tokenHash + "2", expiresAt);
            ev.AcceptInvitation(ev.Invitations.First(i => i.ParticipantEmail == "alice@example.com").Id);
            ev.ClearDomainEvents();

            ev.Cancel();

            var cancelledEvent = Assert.IsType<EventCancelled>(Assert.Single(ev.DomainEvents));
            Assert.Contains("alice@example.com", cancelledEvent.AffectedParticipantEmails);
            Assert.Contains("bob@example.com", cancelledEvent.AffectedParticipantEmails);
        }

        [Fact]
        public void WithDeclinedInvitationsOnly_AffectedEmailsIsEmpty()
        {
            var ev = CreatePublishedEvent();
            var (rawToken, tokenHash, expiresAt) = FakeToken();
            ev.AddInvitation("alice@example.com", rawToken, tokenHash, expiresAt);
            ev.DeclineInvitation(ev.Invitations.First().Id);
            ev.ClearDomainEvents();

            ev.Cancel();

            var cancelledEvent = Assert.IsType<EventCancelled>(Assert.Single(ev.DomainEvents));
            Assert.Empty(cancelledEvent.AffectedParticipantEmails);
        }
    }

    // ── AddInvitation ─────────────────────────────────────────────────────────

    public class AddInvitation
    {
        [Fact]
        public void WhenPublished_CreatesInvitationWithPendingStatus()
        {
            var ev = CreatePublishedEvent();
            var (rawToken, tokenHash, expiresAt) = FakeToken();

            ev.AddInvitation("alice@example.com", rawToken, tokenHash, expiresAt);

            var invitation = Assert.Single(ev.Invitations);
            Assert.Equal(InvitationStatus.Pending, invitation.Status);
            Assert.Equal("alice@example.com", invitation.ParticipantEmail);
            Assert.Equal(tokenHash, invitation.RsvpTokenHash);
            Assert.Equal(expiresAt, invitation.RsvpTokenExpiresAt);
        }

        [Fact]
        public void WhenPublished_RaisesInvitationSentDomainEvent()
        {
            var ev = CreatePublishedEvent();
            var (rawToken, tokenHash, expiresAt) = FakeToken();

            ev.AddInvitation("alice@example.com", rawToken, tokenHash, expiresAt);

            var domainEvent = Assert.IsType<InvitationSent>(Assert.Single(ev.DomainEvents));
            Assert.Equal("alice@example.com", domainEvent.ParticipantEmail);
            Assert.Equal(rawToken, domainEvent.RsvpToken);
            Assert.Equal(ev.Id, domainEvent.EventId);
        }

        [Fact]
        public void NormalizesEmailToLowercase()
        {
            var ev = CreatePublishedEvent();
            var (rawToken, tokenHash, expiresAt) = FakeToken();

            ev.AddInvitation("Alice@Example.COM", rawToken, tokenHash, expiresAt);

            Assert.Equal("alice@example.com", Assert.Single(ev.Invitations).ParticipantEmail);
        }

        [Fact]
        public void WhenDraft_ThrowsDomainException()
        {
            var ev = Event.Create("Event", null, DateTimeOffset.UtcNow.AddDays(1), null, null, "org-1");
            var (rawToken, tokenHash, expiresAt) = FakeToken();

            Assert.Throws<DomainException>(() =>
                ev.AddInvitation("alice@example.com", rawToken, tokenHash, expiresAt));
        }

        [Fact]
        public void WhenCancelled_ThrowsDomainException()
        {
            var ev = CreatePublishedEvent();
            ev.Cancel();
            var (rawToken, tokenHash, expiresAt) = FakeToken();

            Assert.Throws<DomainException>(() =>
                ev.AddInvitation("alice@example.com", rawToken, tokenHash, expiresAt));
        }

        [Fact]
        public void WhenDuplicateEmail_ThrowsDomainException()
        {
            var ev = CreatePublishedEvent();
            var (rawToken, tokenHash, expiresAt) = FakeToken();
            ev.AddInvitation("alice@example.com", rawToken, tokenHash, expiresAt);
            ev.ClearDomainEvents();

            Assert.Throws<DomainException>(() =>
                ev.AddInvitation("alice@example.com", rawToken, tokenHash, expiresAt));
        }

        [Fact]
        public void WhenDuplicateEmailDifferentCase_ThrowsDomainException()
        {
            var ev = CreatePublishedEvent();
            var (rawToken, tokenHash, expiresAt) = FakeToken();
            ev.AddInvitation("alice@example.com", rawToken, tokenHash, expiresAt);
            ev.ClearDomainEvents();

            Assert.Throws<DomainException>(() =>
                ev.AddInvitation("ALICE@EXAMPLE.COM", rawToken, tokenHash + "2", expiresAt));
        }

        [Fact]
        public void AfterCancelledInvitation_AllowsReinviteSameEmail()
        {
            var ev = CreatePublishedEvent();
            var (rawToken, tokenHash, expiresAt) = FakeToken();
            ev.AddInvitation("alice@example.com", rawToken, tokenHash, expiresAt);
            var invitationId = ev.Invitations.First().Id;
            ev.CancelInvitation(invitationId);
            ev.ClearDomainEvents();

            // Should not throw — cancelled invitation allows reinvite
            ev.AddInvitation("alice@example.com", rawToken, tokenHash + "new", expiresAt);

            Assert.Equal(2, ev.Invitations.Count);
        }
    }

    // ── AcceptInvitation ──────────────────────────────────────────────────────

    public class AcceptInvitation
    {
        [Fact]
        public void WhenPending_ChangesStatusToAccepted()
        {
            var ev = CreatePublishedEvent();
            var (rawToken, tokenHash, expiresAt) = FakeToken();
            ev.AddInvitation("alice@example.com", rawToken, tokenHash, expiresAt);
            var invitationId = ev.Invitations.First().Id;
            ev.ClearDomainEvents();

            ev.AcceptInvitation(invitationId);

            Assert.Equal(InvitationStatus.Accepted, ev.Invitations.First().Status);
        }

        [Fact]
        public void WhenPending_ClearsToken()
        {
            var ev = CreatePublishedEvent();
            var (rawToken, tokenHash, expiresAt) = FakeToken();
            ev.AddInvitation("alice@example.com", rawToken, tokenHash, expiresAt);
            var invitationId = ev.Invitations.First().Id;

            ev.AcceptInvitation(invitationId);

            var invitation = ev.Invitations.First();
            Assert.Null(invitation.RsvpTokenHash);
            Assert.Null(invitation.RsvpTokenExpiresAt);
        }

        [Fact]
        public void WhenPending_RaisesInvitationRespondedDomainEvent()
        {
            var ev = CreatePublishedEvent();
            var (rawToken, tokenHash, expiresAt) = FakeToken();
            ev.AddInvitation("alice@example.com", rawToken, tokenHash, expiresAt);
            var invitationId = ev.Invitations.First().Id;
            ev.ClearDomainEvents();

            ev.AcceptInvitation(invitationId);

            var domainEvent = Assert.IsType<InvitationResponded>(Assert.Single(ev.DomainEvents));
            Assert.Equal(InvitationStatus.Accepted, domainEvent.Response);
            Assert.Equal("alice@example.com", domainEvent.ParticipantEmail);
        }

        [Fact]
        public void WhenAtCapacity_ThrowsDomainException()
        {
            var ev = CreatePublishedEvent(capacity: 1);
            var (rawToken, tokenHash, expiresAt) = FakeToken();
            ev.AddInvitation("alice@example.com", rawToken, tokenHash, expiresAt);
            ev.AddInvitation("bob@example.com", rawToken + "2", tokenHash + "2", expiresAt);
            ev.AcceptInvitation(ev.Invitations.First(i => i.ParticipantEmail == "alice@example.com").Id);
            ev.ClearDomainEvents();

            Assert.Throws<DomainException>(() =>
                ev.AcceptInvitation(ev.Invitations.First(i => i.ParticipantEmail == "bob@example.com").Id));
        }

        [Fact]
        public void WithNonExistentInvitationId_ThrowsDomainException()
        {
            var ev = CreatePublishedEvent();

            Assert.Throws<DomainException>(() => ev.AcceptInvitation(Guid.NewGuid()));
        }

        [Fact]
        public void WhenAlreadyAccepted_ThrowsDomainException()
        {
            var ev = CreatePublishedEvent();
            var (rawToken, tokenHash, expiresAt) = FakeToken();
            ev.AddInvitation("alice@example.com", rawToken, tokenHash, expiresAt);
            var invitationId = ev.Invitations.First().Id;
            ev.AcceptInvitation(invitationId);
            ev.ClearDomainEvents();

            Assert.Throws<DomainException>(() => ev.AcceptInvitation(invitationId));
        }
    }

    // ── DeclineInvitation ─────────────────────────────────────────────────────

    public class DeclineInvitation
    {
        [Fact]
        public void WhenPending_ChangesStatusToDeclined()
        {
            var ev = CreatePublishedEvent();
            var (rawToken, tokenHash, expiresAt) = FakeToken();
            ev.AddInvitation("alice@example.com", rawToken, tokenHash, expiresAt);
            var invitationId = ev.Invitations.First().Id;
            ev.ClearDomainEvents();

            ev.DeclineInvitation(invitationId);

            Assert.Equal(InvitationStatus.Declined, ev.Invitations.First().Status);
        }

        [Fact]
        public void WhenPending_ClearsToken()
        {
            var ev = CreatePublishedEvent();
            var (rawToken, tokenHash, expiresAt) = FakeToken();
            ev.AddInvitation("alice@example.com", rawToken, tokenHash, expiresAt);
            var invitationId = ev.Invitations.First().Id;

            ev.DeclineInvitation(invitationId);

            var invitation = ev.Invitations.First();
            Assert.Null(invitation.RsvpTokenHash);
            Assert.Null(invitation.RsvpTokenExpiresAt);
        }

        [Fact]
        public void WhenPending_RaisesInvitationRespondedDomainEvent()
        {
            var ev = CreatePublishedEvent();
            var (rawToken, tokenHash, expiresAt) = FakeToken();
            ev.AddInvitation("alice@example.com", rawToken, tokenHash, expiresAt);
            var invitationId = ev.Invitations.First().Id;
            ev.ClearDomainEvents();

            ev.DeclineInvitation(invitationId);

            var domainEvent = Assert.IsType<InvitationResponded>(Assert.Single(ev.DomainEvents));
            Assert.Equal(InvitationStatus.Declined, domainEvent.Response);
        }

        [Fact]
        public void WhenAlreadyDeclined_ThrowsDomainException()
        {
            var ev = CreatePublishedEvent();
            var (rawToken, tokenHash, expiresAt) = FakeToken();
            ev.AddInvitation("alice@example.com", rawToken, tokenHash, expiresAt);
            var invitationId = ev.Invitations.First().Id;
            ev.DeclineInvitation(invitationId);
            ev.ClearDomainEvents();

            Assert.Throws<DomainException>(() => ev.DeclineInvitation(invitationId));
        }
    }

    // ── CancelInvitation ──────────────────────────────────────────────────────

    public class CancelInvitation
    {
        [Fact]
        public void WhenPending_ChangesStatusToCancelled()
        {
            var ev = CreatePublishedEvent();
            var (rawToken, tokenHash, expiresAt) = FakeToken();
            ev.AddInvitation("alice@example.com", rawToken, tokenHash, expiresAt);
            var invitationId = ev.Invitations.First().Id;
            ev.ClearDomainEvents();

            ev.CancelInvitation(invitationId);

            Assert.Equal(InvitationStatus.Cancelled, ev.Invitations.First().Status);
        }

        [Fact]
        public void WhenPending_ClearsToken()
        {
            var ev = CreatePublishedEvent();
            var (rawToken, tokenHash, expiresAt) = FakeToken();
            ev.AddInvitation("alice@example.com", rawToken, tokenHash, expiresAt);
            var invitationId = ev.Invitations.First().Id;

            ev.CancelInvitation(invitationId);

            var invitation = ev.Invitations.First();
            Assert.Null(invitation.RsvpTokenHash);
            Assert.Null(invitation.RsvpTokenExpiresAt);
        }

        [Fact]
        public void WhenAccepted_ThrowsDomainException()
        {
            var ev = CreatePublishedEvent();
            var (rawToken, tokenHash, expiresAt) = FakeToken();
            ev.AddInvitation("alice@example.com", rawToken, tokenHash, expiresAt);
            var invitationId = ev.Invitations.First().Id;
            ev.AcceptInvitation(invitationId);
            ev.ClearDomainEvents();

            Assert.Throws<DomainException>(() => ev.CancelInvitation(invitationId));
        }

        [Fact]
        public void WhenEventCancelled_ThrowsDomainException()
        {
            var ev = CreatePublishedEvent();
            var (rawToken, tokenHash, expiresAt) = FakeToken();
            ev.AddInvitation("alice@example.com", rawToken, tokenHash, expiresAt);
            var invitationId = ev.Invitations.First().Id;
            ev.Cancel();
            ev.ClearDomainEvents();

            Assert.Throws<DomainException>(() => ev.CancelInvitation(invitationId));
        }
    }

    // ── ReissueInvitationToken ────────────────────────────────────────────────

    public class ReissueInvitationToken
    {
        [Fact]
        public void WhenPending_UpdatesTokenAndRaisesInvitationSentDomainEvent()
        {
            var ev = CreatePublishedEvent();
            var (rawToken, tokenHash, expiresAt) = FakeToken();
            ev.AddInvitation("alice@example.com", rawToken, tokenHash, expiresAt);
            var invitationId = ev.Invitations.First().Id;
            ev.ClearDomainEvents();

            var newExpiry = DateTimeOffset.UtcNow.AddHours(72);
            ev.ReissueInvitationToken(invitationId, "new-raw-token", "new-hash", newExpiry);

            var invitation = ev.Invitations.First();
            Assert.Equal("new-hash", invitation.RsvpTokenHash);
            Assert.Equal(newExpiry, invitation.RsvpTokenExpiresAt);

            var domainEvent = Assert.IsType<InvitationSent>(Assert.Single(ev.DomainEvents));
            Assert.Equal("new-raw-token", domainEvent.RsvpToken);
        }

        [Fact]
        public void WhenAccepted_ThrowsDomainException()
        {
            var ev = CreatePublishedEvent();
            var (rawToken, tokenHash, expiresAt) = FakeToken();
            ev.AddInvitation("alice@example.com", rawToken, tokenHash, expiresAt);
            var invitationId = ev.Invitations.First().Id;
            ev.AcceptInvitation(invitationId);

            Assert.Throws<DomainException>(() =>
                ev.ReissueInvitationToken(invitationId, "new-raw", "new-hash", DateTimeOffset.UtcNow.AddHours(72)));
        }
    }
}
