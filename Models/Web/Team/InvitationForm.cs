public class InvitationForm
{
    public long InvitationID { get; set; }
    public long TeamID { get; set; } = -1;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Number { get; set; } = string.Empty;
    public long StatusID { get; set; } = 1;
    public string Emails { get; set; } = string.Empty;
    public string EmergencyContactInfo { get; set; } = string.Empty;
    public bool IsWebmaster { get; set; }
    public bool IsExecutive { get; set; }
    public bool IsManager { get; set; }
    public bool IsScorer { get; set; }
    public bool IsReporter { get; set; }

    public bool IsNewInvitation { get; set; } = true;
    public bool UserExists { get; set; }
    public bool PlayerExists { get; set; }

    public InvitationForm() { }

    public InvitationForm(Invitation i)
    {

        IsNewInvitation = false;

        InvitationID = i.ID;
        TeamID = i.TeamID;
        StatusID = i.StatusID;
        EmergencyContactInfo = i.EmergencyContactInfo ?? string.Empty;

        if (i.Player != null)
        {
            PlayerExists = true;
            FirstName = i.Player.FirstName;
            LastName = i.Player.LastName;
            Number = i.Player.Number ?? string.Empty;
        }

        if (i.User != null)
        {
            UserExists = true;
            Emails = string.Join(',', i.User.UserLogins.Select(ul => ul.Email));
            if (i.User.UserRoles.Count > 0 && i.User.UserRoles.All(ur => ur.Role != null))
            {
                IsWebmaster = i.User.UserRoles.Any(ur => ur.Role!.Name == "Webmaster");
                IsExecutive = i.User.UserRoles.Any(ur => ur.Role!.Name == "Executive");
            }
            
        }
        else
        {
            Emails = string.Join(',', i.InvitationEmails.Select(ie => ie.Email));
        }
        if (i.InvitationRoles.All(ir => ir.Role != null))
        {
            IsManager = i.InvitationRoles.Any(ir => ir.Role!.Name == "Manager");
            IsScorer = i.InvitationRoles.Any(ir => ir.Role!.Name == "Scorer");
            IsReporter = i.InvitationRoles.Any(ir => ir.Role!.Name == "Reporter");
        }
    }

    public void UpdateExistingInvitation(ref Invitation i)
    {
        if (FirstName + LastName + Number != string.Empty && i.Player == null)
        {
            i.Player = new Player()
            {
                FirstName = FirstName,
                LastName = LastName,
                Number = Number,
                ShortCode = string.Empty,
            };
        }
        if (FirstName + LastName + Number != string.Empty)
        {
            i.Player!.FirstName = FirstName; // i.Player must exist in the above condition, because if it doesn't, it's created directly above
            i.Player.LastName = LastName;
            i.Player.Number = Number;
        }
        // FORM VALIDATION NOTE: IF PLAYER EXISTS, THEY MUST HAVE FIRST+LAST; NUMBER IS ALWAYS OPTIONAL

        if (!UserExists)
        {
            var formEmails = Emails.Split(",");
            var emailsToAdd = formEmails.ToList();
            var emailsToRemove = new List<InvitationEmail>();
            foreach (var e in i.InvitationEmails)
            {
                emailsToAdd.Remove(e.Email); //don't add duplicate
                if (!formEmails.Contains(e.Email))
                {
                    emailsToRemove.Add(e);
                }
            }
            foreach (var e in emailsToAdd)
            {
                i.InvitationEmails.Add(new InvitationEmail()
                {
                    InvitationID = i.ID,
                    Email = e
                });
            }
            foreach (var e in emailsToRemove)
            {
                i.InvitationEmails.Remove(e);
            }
        }

        if (i.User != null && IsWebmaster != i.User.UserRoles.Any(ur => ur.RoleID == (long)Roles.Webmaster))
        {
            if (IsWebmaster)
            {
                i.User.UserRoles.Add(new UserRole()
                {
                    UserID = i.UserID ?? default,
                    RoleID = (long)Roles.Webmaster,
                });
            }
            else
            {
                i.User.UserRoles.Remove(i.User.UserRoles.First(ur => ur.RoleID == (long)Roles.Webmaster));
            }
        }
        if (i.User != null && IsExecutive != i.User.UserRoles.Any(ur => ur.RoleID == (long)Roles.Executive))
        {
            if (IsExecutive)
            {
                i.User.UserRoles.Add(new UserRole()
                {
                    UserID = i.UserID ?? default,
                    RoleID = (long)Roles.Executive,
                });
            }
            else
            {
                i.User.UserRoles.Remove(i.User.UserRoles.First(ur => ur.RoleID == (long)Roles.Executive));
            }
        }
        if (IsManager != i.InvitationRoles.Any(ir => ir.RoleID == (long)Roles.Manager))
        {
            if (IsManager)
            {
                i.InvitationRoles.Add(new InvitationRole()
                {
                    InvitationID = i.ID,
                    RoleID = (long)Roles.Manager,
                });
            }
            else
            {
                i.InvitationRoles.Remove(i.InvitationRoles.First(ir => ir.RoleID == (long)Roles.Manager));
            }
        }
        if (IsScorer != i.InvitationRoles.Any(ir => ir.RoleID == (long)Roles.Scorer))
        {
            if (IsScorer)
            {
                i.InvitationRoles.Add(new InvitationRole()
                {
                    InvitationID = i.ID,
                    RoleID = (long)Roles.Scorer,
                });
            }
            else
            {
                i.InvitationRoles.Remove(i.InvitationRoles.First(ir => ir.RoleID == (long)Roles.Scorer));
            }
        }
        if (IsReporter != i.InvitationRoles.Any(ir => ir.RoleID == (long)Roles.Reporter))
        {
            if (IsReporter)
            {
                i.InvitationRoles.Add(new InvitationRole()
                {
                    InvitationID = i.ID,
                    RoleID = (long)Roles.Reporter,
                });
            }
            else
            {
                i.InvitationRoles.Remove(i.InvitationRoles.First(ir => ir.RoleID == (long)Roles.Reporter));
            }
        }

        i.EmergencyContactInfo = EmergencyContactInfo;
        i.StatusID = StatusID;
    }

    /// Map this InvitationForm's values to a new Invitation record (used for new Invitations only)
    public Invitation ToInvitation()
    {
        var invitation = new Invitation
        {
            EmergencyContactInfo = EmergencyContactInfo
        };

        if (PlayerExists)
        {
            // Create player
            // Set name
            invitation.Player = new Player()
            {
                FirstName = FirstName,
                LastName = LastName,
                ShortCode = string.Empty,
            };
            // Set number (if set)
            if (!string.IsNullOrEmpty(Number))
            {
                invitation.Player.Number = Number;
            }
            invitation.StatusID = StatusID;
        }
        else
        {
            invitation.StatusID = (long)InvitationStatuses.Other;
        }


        invitation.TeamID = TeamID;

        if (IsScorer)
        {
            invitation.InvitationRoles.Add(new InvitationRole() { Invitation = invitation, RoleID = (long)Roles.Scorer });
        }
        if (IsManager)
        {
            invitation.InvitationRoles.Add(new InvitationRole() { Invitation = invitation, RoleID = (long)Roles.Manager });
        }
        if (IsReporter)
        {
            invitation.InvitationRoles.Add(new InvitationRole() { Invitation = invitation, RoleID = (long)Roles.Reporter });
        }

        foreach (var email in Emails.Split(','))
        {
            invitation.InvitationEmails.Add(new InvitationEmail() { Invitation = invitation, Email = email });
        }

        return invitation;
    }

}