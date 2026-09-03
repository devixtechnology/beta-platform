using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BetaPlatform.Migrations
{
    /// <summary>
    /// Data-only migration (no schema change) that provisions the five client sign-ins the client
    /// asked for in 004 feedback item 1, so a deployment reaches SC-004 without anyone typing five
    /// forms by hand: <c>client1@beta.local</c> … <c>client5@beta.local</c>, each in the
    /// <c>Client</c> role and active.
    ///
    /// Only the Identity password hashes are stored here — the plaintexts were generated once,
    /// are distinct per account, and are handed over out of band (FR-006: nothing publicly known
    /// ships in the repository). Holders are expected to change them at <c>/Account/ChangePassword</c>;
    /// an administrator can reset any of them at <c>/Users/ResetPassword</c>.
    ///
    /// The account inserts are idempotent (<c>INSERT IGNORE</c> against the Identity unique indexes),
    /// so re-running them on a database where an operator already created these accounts changes
    /// nothing and never overwrites a password that has since been changed.
    ///
    /// It also retires the seeded administrator credential (004 feedback item 1, FR-006): the
    /// <c>admin@beta.local</c> row gets a new password hash and a fresh security stamp, which signs
    /// out every live cookie session and invalidates every issued bearer token on its next request
    /// (the security-stamp check in <c>OnTokenValidated</c>). Two consequences to know about:
    ///
    /// <list type="bullet">
    /// <item>It is <b>not reversible</b>. <c>Down</c> cannot restore a hash it never saw, so rolling
    /// back leaves the administrator on this password; change it at <c>/Account/ChangePassword</c>.</item>
    /// <item>A migration cannot tell a freshly-seeded administrator from one whose password was
    /// already hardened by hand. On any database where this migration has not yet run, it
    /// <b>overwrites</b> the current administrator password. On a brand-new database it instead
    /// matches no rows — migrations run before <c>DbSeeder</c> creates the administrator — and that
    /// account then takes its password from <c>AdminSeed:Password</c> as usual.</item>
    /// </list>
    /// </summary>
    public partial class SeedClientAccounts : Migration
    {
        /// <summary>Placeholder accounts; rename through /Users/Edit once the client supplies real addresses.</summary>
        private static readonly (string Id, string Email, string FullName, string Hash, string SecurityStamp, string ConcurrencyStamp)[] Clients =
        {
            ("887dad95-2e87-4754-8960-204a2ff91e85", "client1@beta.local", "Client User 1",
             "AQAAAAIAAYagAAAAELn+JpcmO0N9SNtJpuHG0m4WWh0aTJPoXKhk/ZNpSXBsc3ZASjhHKf+DmwANhS3rXw==",
             "d622cf70-11fe-4de7-9a21-bc2fd8f88259", "0a762786-2550-4664-a4c3-20ace4e71a82"),
            ("404fd79b-5b47-4ff8-9081-2a6d236b9dfd", "client2@beta.local", "Client User 2",
             "AQAAAAIAAYagAAAAEFpLAL/qvtLK+9XvM1vIjmhan4HokdbRumTE4gALU/jhwdgryTb3zvOcLtvKznmtKg==",
             "80ac03f4-8b81-4c87-9d18-e1c5e7d3a169", "0674e322-97e1-4c67-8ec1-521a1b26dd85"),
            ("e8627edb-662b-4e60-b911-fb77c529bd76", "client3@beta.local", "Client User 3",
             "AQAAAAIAAYagAAAAEIDEGuBjwVhFSwU1CY7sBNedKjmvhXsabrbDbeGO69O1FKbt9KW7PS3DopJeYiUVUQ==",
             "0ff5e6ce-b9a9-40b0-8bd7-dd7be9c152dd", "6e780644-6ee2-426e-8b72-16b2937cb7d4"),
            ("a16cf30d-aabd-4280-80f7-c9d2bd3124dd", "client4@beta.local", "Client User 4",
             "AQAAAAIAAYagAAAAEE3tyaEDcCh8TfbQsbdJOALHUQDihZQqAKfGuQfCIb6jyEaNpt4gyr8K0GODrDSV1w==",
             "091da010-ec03-40b4-a438-b11dd12f663a", "7de8da16-b3f5-4d35-8107-79d67351857a"),
            ("668a7486-2218-4eb3-a182-90e7c4e33476", "client5@beta.local", "Client User 5",
             "AQAAAAIAAYagAAAAEAdHi3B/GPkhYQAiBt+EsIc+4+BRaGGeZ2FenEv7Cfoi95UWjDtAxChtDu72eIyZAA==",
             "79b1f1cb-2701-4222-9a8f-8e7f332db80e", "64723bc1-555e-4312-b7fd-6dd4034b6cae")
        };

        private const string ClientRoleId = "3f2b6d6c-9d4e-4b8a-9a1c-6e0d5f4a7c21";
        private const string SeededAt = "2026-09-03 00:00:00";

        /// <summary>Replacement administrator credential. Hash only — the plaintext was generated
        /// once and handed over out of band, exactly as for the client accounts above.</summary>
        private const string AdminEmailNormalized = "ADMIN@BETA.LOCAL";
        private const string AdminPasswordHash =
            "AQAAAAIAAYagAAAAEIUh1hkuiMMMI0AtTNUNZYkCbxdGv0Yhb05PhhGfR7k5RDjW3v7CH72R6Z8hPpgoKg==";
        private const string AdminSecurityStamp = "8efb9d77-724b-4c9a-82bb-081d87e5aef0";
        private const string AdminConcurrencyStamp = "d8b3978e-0918-41b9-abb6-d9a5e6a88bfd";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The Client role is normally created by DbSeeder at startup, but a migration must not
            // depend on that having run. The unique index on NormalizedName makes this a no-op when
            // the role already exists under a different Id.
            migrationBuilder.Sql(
                "INSERT IGNORE INTO `AspNetRoles` (`Id`, `Name`, `NormalizedName`, `ConcurrencyStamp`) " +
                $"VALUES ('{ClientRoleId}', 'Client', 'CLIENT', UUID());");

            foreach (var c in Clients)
            {
                var normalized = c.Email.ToUpperInvariant();
                migrationBuilder.Sql(
                    "INSERT IGNORE INTO `AspNetUsers` " +
                    "(`Id`, `FullName`, `CreatedAt`, `IsActive`, `UserName`, `NormalizedUserName`, `Email`, " +
                    " `NormalizedEmail`, `EmailConfirmed`, `PasswordHash`, `SecurityStamp`, `ConcurrencyStamp`, " +
                    " `PhoneNumber`, `PhoneNumberConfirmed`, `TwoFactorEnabled`, `LockoutEnd`, `LockoutEnabled`, " +
                    " `AccessFailedCount`) VALUES (" +
                    $"'{c.Id}', '{c.FullName}', '{SeededAt}', 1, '{c.Email}', '{normalized}', '{c.Email}', " +
                    $"'{normalized}', 1, '{c.Hash}', '{c.SecurityStamp}', '{c.ConcurrencyStamp}', " +
                    "NULL, 0, 0, NULL, 1, 0);");
            }

            // Linked by lookup rather than by the Id above, so the accounts land in whichever Client
            // role row the database already has.
            var emails = string.Join(", ", Clients.Select(c => $"'{c.Email.ToUpperInvariant()}'"));
            migrationBuilder.Sql(
                "INSERT IGNORE INTO `AspNetUserRoles` (`UserId`, `RoleId`) " +
                "SELECT u.`Id`, r.`Id` FROM `AspNetUsers` u CROSS JOIN `AspNetRoles` r " +
                $"WHERE u.`NormalizedEmail` IN ({emails}) AND r.`NormalizedName` = 'CLIENT';");

            // Retire the seeded administrator password. The rotated SecurityStamp is what makes the
            // change take effect on the next request rather than at the next sign-out, for both the
            // cookie SecurityStampValidator and the bearer handler. Matches nothing on a database
            // whose administrator has not been created yet, which is the correct no-op.
            migrationBuilder.Sql(
                "UPDATE `AspNetUsers` SET " +
                $"`PasswordHash` = '{AdminPasswordHash}', " +
                $"`SecurityStamp` = '{AdminSecurityStamp}', " +
                $"`ConcurrencyStamp` = '{AdminConcurrencyStamp}' " +
                $"WHERE `NormalizedEmail` = '{AdminEmailNormalized}';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Removes only these five accounts. The Client role stays: DbSeeder owns it, and other
            // accounts may hold it. The administrator password is deliberately left alone — the hash
            // it replaced is unknown here, and restoring a weaker credential on a rollback would be
            // the wrong direction anyway.
            var emails = string.Join(", ", Clients.Select(c => $"'{c.Email.ToUpperInvariant()}'"));
            migrationBuilder.Sql(
                "DELETE FROM `AspNetUserRoles` WHERE `UserId` IN " +
                $"(SELECT `Id` FROM `AspNetUsers` WHERE `NormalizedEmail` IN ({emails}));");
            migrationBuilder.Sql(
                $"DELETE FROM `AspNetUsers` WHERE `NormalizedEmail` IN ({emails});");
        }
    }
}
