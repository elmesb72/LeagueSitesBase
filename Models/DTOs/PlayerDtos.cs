using Facet;

[Facet(typeof(Player),
    Include = [nameof(Player.ID), nameof(Player.FirstName), nameof(Player.LastName),
               nameof(Player.Name), nameof(Player.Number), nameof(Player.ShortCode)])]
public partial record PlayerSummaryDto;

[Facet(typeof(Player),
    Include = [nameof(Player.ID), nameof(Player.FirstName), nameof(Player.LastName),
               nameof(Player.Name), nameof(Player.Number), nameof(Player.ShortCode),
               nameof(Player.Bio), nameof(Player.Invitations), nameof(Player.Socials)],
    NestedFacets = [typeof(PlayerBioDto), typeof(PlayerInvitationDto), typeof(PlayerSocialDto)])]
public partial record PlayerDetailDto;

[Facet(typeof(PlayerBio),
    Include = [nameof(PlayerBio.Bats), nameof(PlayerBio.Throws), nameof(PlayerBio.Positions),
               nameof(PlayerBio.Height), nameof(PlayerBio.HeightImperial),
               nameof(PlayerBio.Weight), nameof(PlayerBio.WeightImperial),
               nameof(PlayerBio.Birthdate), nameof(PlayerBio.From), nameof(PlayerBio.ReferredBy)])]
public partial record PlayerBioDto;

[Facet(typeof(Invitation),
    Include = [nameof(Invitation.Team), nameof(Invitation.Status)],
    NestedFacets = [typeof(TeamSummaryDto), typeof(InvitationStatusDto)])]
public partial record PlayerInvitationDto;

[Facet(typeof(InvitationStatus),
    Include = [nameof(InvitationStatus.Name)])]
public partial record InvitationStatusDto;

[Facet(typeof(PlayerSocial),
    Include = [nameof(PlayerSocial.Account), nameof(PlayerSocial.Platform)],
    NestedFacets = [typeof(SocialPlatformDto)])]
public partial record PlayerSocialDto;

[Facet(typeof(SocialPlatform),
    Include = [nameof(SocialPlatform.Name)])]
public partial record SocialPlatformDto;
