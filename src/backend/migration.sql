CREATE TABLE IF NOT EXISTS `__EFMigrationsHistory` (
    `MigrationId` varchar(150) CHARACTER SET utf8mb4 NOT NULL,
    `ProductVersion` varchar(32) CHARACTER SET utf8mb4 NOT NULL,
    CONSTRAINT `PK___EFMigrationsHistory` PRIMARY KEY (`MigrationId`)
) CHARACTER SET=utf8mb4;

START TRANSACTION;

ALTER DATABASE CHARACTER SET utf8mb4;

CREATE TABLE `ActivityEvents` (
    `Id` varchar(255) CHARACTER SET utf8mb4 NOT NULL,
    `Type` varchar(50) CHARACTER SET utf8mb4 NOT NULL,
    `Title` varchar(200) CHARACTER SET utf8mb4 NOT NULL,
    `Description` longtext CHARACTER SET utf8mb4 NOT NULL,
    `Severity` varchar(20) CHARACTER SET utf8mb4 NULL,
    `Icon` varchar(50) CHARACTER SET utf8mb4 NULL,
    `Timestamp` datetime(6) NOT NULL,
    CONSTRAINT `PK_ActivityEvents` PRIMARY KEY (`Id`)
) CHARACTER SET=utf8mb4;

CREATE TABLE `Alerts` (
    `Id` varchar(255) CHARACTER SET utf8mb4 NOT NULL,
    `Title` varchar(200) CHARACTER SET utf8mb4 NOT NULL,
    `Description` longtext CHARACTER SET utf8mb4 NOT NULL,
    `Severity` varchar(20) CHARACTER SET utf8mb4 NOT NULL,
    `Confidence` int NOT NULL,
    `Timestamp` longtext CHARACTER SET utf8mb4 NOT NULL,
    `ProcessName` varchar(100) CHARACTER SET utf8mb4 NOT NULL,
    `Resolved` tinyint(1) NOT NULL,
    CONSTRAINT `PK_Alerts` PRIMARY KEY (`Id`)
) CHARACTER SET=utf8mb4;

CREATE TABLE `Appeals` (
    `Id` varchar(255) CHARACTER SET utf8mb4 NOT NULL,
    `Player` varchar(100) CHARACTER SET utf8mb4 NOT NULL,
    `PlayerId` varchar(255) CHARACTER SET utf8mb4 NULL,
    `BanId` varchar(255) CHARACTER SET utf8mb4 NULL,
    `Reason` longtext CHARACTER SET utf8mb4 NOT NULL,
    `BanType` varchar(20) CHARACTER SET utf8mb4 NOT NULL,
    `Status` varchar(20) CHARACTER SET utf8mb4 NOT NULL,
    `Date` longtext CHARACTER SET utf8mb4 NOT NULL,
    `Reviewer` varchar(100) CHARACTER SET utf8mb4 NOT NULL,
    CONSTRAINT `PK_Appeals` PRIMARY KEY (`Id`)
) CHARACTER SET=utf8mb4;

CREATE TABLE `AuditLogEntries` (
    `Id` varchar(255) CHARACTER SET utf8mb4 NOT NULL,
    `Action` varchar(50) CHARACTER SET utf8mb4 NOT NULL,
    `User` varchar(100) CHARACTER SET utf8mb4 NOT NULL,
    `Target` varchar(100) CHARACTER SET utf8mb4 NOT NULL,
    `Details` longtext CHARACTER SET utf8mb4 NOT NULL,
    `Timestamp` varchar(255) CHARACTER SET utf8mb4 NOT NULL,
    `Ip` longtext CHARACTER SET utf8mb4 NOT NULL,
    CONSTRAINT `PK_AuditLogEntries` PRIMARY KEY (`Id`)
) CHARACTER SET=utf8mb4;

CREATE TABLE `BanEntries` (
    `Id` varchar(255) CHARACTER SET utf8mb4 NOT NULL,
    `Player` varchar(100) CHARACTER SET utf8mb4 NOT NULL,
    `Reason` longtext CHARACTER SET utf8mb4 NOT NULL,
    `Type` varchar(20) CHARACTER SET utf8mb4 NOT NULL,
    `IssuedBy` varchar(100) CHARACTER SET utf8mb4 NOT NULL,
    `IssuedAt` longtext CHARACTER SET utf8mb4 NOT NULL,
    `Active` tinyint(1) NOT NULL,
    `Appeals` int NOT NULL,
    CONSTRAINT `PK_BanEntries` PRIMARY KEY (`Id`)
) CHARACTER SET=utf8mb4;

CREATE TABLE `Devices` (
    `DeviceId` varchar(255) CHARACTER SET utf8mb4 NOT NULL,
    `DeviceName` varchar(100) CHARACTER SET utf8mb4 NOT NULL,
    `OsVersion` varchar(50) CHARACTER SET utf8mb4 NOT NULL,
    `Fingerprint` varchar(256) CHARACTER SET utf8mb4 NOT NULL,
    `TrustScore` int NOT NULL,
    `IsVerified` tinyint(1) NOT NULL,
    `UserId` varchar(255) CHARACTER SET utf8mb4 NULL,
    `FirstSeen` datetime(6) NOT NULL,
    `LastSeen` datetime(6) NOT NULL,
    CONSTRAINT `PK_Devices` PRIMARY KEY (`DeviceId`)
) CHARACTER SET=utf8mb4;

CREATE TABLE `ModeratorReports` (
    `Id` varchar(255) CHARACTER SET utf8mb4 NOT NULL,
    `PlayerName` varchar(100) CHARACTER SET utf8mb4 NOT NULL,
    `Reason` varchar(200) CHARACTER SET utf8mb4 NOT NULL,
    `ReporterName` varchar(100) CHARACTER SET utf8mb4 NOT NULL,
    `Status` varchar(20) CHARACTER SET utf8mb4 NOT NULL,
    `Severity` varchar(20) CHARACTER SET utf8mb4 NOT NULL,
    `EvidenceCount` int NOT NULL,
    `CreatedAt` datetime(6) NOT NULL,
    `AssignedModerator` varchar(100) CHARACTER SET utf8mb4 NULL,
    `NotesJson` longtext CHARACTER SET utf8mb4 NOT NULL,
    CONSTRAINT `PK_ModeratorReports` PRIMARY KEY (`Id`)
) CHARACTER SET=utf8mb4;

CREATE TABLE `RefreshTokens` (
    `Id` varchar(255) CHARACTER SET utf8mb4 NOT NULL,
    `Token` varchar(128) CHARACTER SET utf8mb4 NOT NULL,
    `UserId` varchar(255) CHARACTER SET utf8mb4 NOT NULL,
    `CreatedAt` datetime(6) NOT NULL,
    `ExpiresAt` datetime(6) NOT NULL,
    `IsRevoked` tinyint(1) NOT NULL,
    CONSTRAINT `PK_RefreshTokens` PRIMARY KEY (`Id`)
) CHARACTER SET=utf8mb4;

CREATE TABLE `Sessions` (
    `SessionId` varchar(255) CHARACTER SET utf8mb4 NOT NULL,
    `UserId` varchar(255) CHARACTER SET utf8mb4 NOT NULL,
    `DeviceId` varchar(100) CHARACTER SET utf8mb4 NOT NULL,
    `IpAddress` varchar(50) CHARACTER SET utf8mb4 NOT NULL,
    `CreatedAt` datetime(6) NOT NULL,
    `LastActivity` datetime(6) NULL,
    `IsActive` tinyint(1) NOT NULL,
    CONSTRAINT `PK_Sessions` PRIMARY KEY (`SessionId`)
) CHARACTER SET=utf8mb4;

CREATE TABLE `TimelineEvents` (
    `Id` varchar(255) CHARACTER SET utf8mb4 NOT NULL,
    `Type` varchar(50) CHARACTER SET utf8mb4 NOT NULL,
    `Title` varchar(200) CHARACTER SET utf8mb4 NOT NULL,
    `Description` longtext CHARACTER SET utf8mb4 NOT NULL,
    `Severity` varchar(20) CHARACTER SET utf8mb4 NOT NULL,
    `Category` varchar(50) CHARACTER SET utf8mb4 NULL,
    `Confidence` double NULL,
    `Timestamp` datetime(6) NOT NULL,
    CONSTRAINT `PK_TimelineEvents` PRIMARY KEY (`Id`)
) CHARACTER SET=utf8mb4;

CREATE TABLE `Users` (
    `Id` varchar(255) CHARACTER SET utf8mb4 NOT NULL,
    `Username` varchar(50) CHARACTER SET utf8mb4 NOT NULL,
    `PasswordHash` longtext CHARACTER SET utf8mb4 NOT NULL,
    `DisplayName` varchar(100) CHARACTER SET utf8mb4 NOT NULL,
    `Role` varchar(20) CHARACTER SET utf8mb4 NOT NULL,
    `TrustScore` int NOT NULL,
    `Level` int NOT NULL,
    `Status` varchar(20) CHARACTER SET utf8mb4 NOT NULL,
    `Avatar` longtext CHARACTER SET utf8mb4 NULL,
    `Email` longtext CHARACTER SET utf8mb4 NULL,
    `Xp` int NOT NULL,
    `NextLevelXp` int NOT NULL,
    `HardwareId` varchar(256) CHARACTER SET utf8mb4 NULL,
    `GamePath` varchar(1024) CHARACTER SET utf8mb4 NULL,
    `CreatedAt` datetime(6) NOT NULL,
    `LastLoginAt` datetime(6) NULL,
    CONSTRAINT `PK_Users` PRIMARY KEY (`Id`)
) CHARACTER SET=utf8mb4;

CREATE TABLE `WhitelistEntries` (
    `Id` varchar(255) CHARACTER SET utf8mb4 NOT NULL,
    `Entry` varchar(500) CHARACTER SET utf8mb4 NOT NULL,
    `Type` varchar(20) CHARACTER SET utf8mb4 NOT NULL,
    `AddedBy` varchar(100) CHARACTER SET utf8mb4 NOT NULL,
    `AddedAt` longtext CHARACTER SET utf8mb4 NOT NULL,
    `Reason` longtext CHARACTER SET utf8mb4 NOT NULL,
    CONSTRAINT `PK_WhitelistEntries` PRIMARY KEY (`Id`)
) CHARACTER SET=utf8mb4;

CREATE TABLE `DetectionFingerprints` (
    `Id` varchar(36) CHARACTER SET utf8mb4 NOT NULL,
    `Fingerprint` varchar(64) CHARACTER SET utf8mb4 NOT NULL,
    `PlayerId` varchar(64) CHARACTER SET utf8mb4 NULL,
    `Category` varchar(32) CHARACTER SET utf8mb4 NOT NULL,
    `FirstSeenAt` datetime(6) NOT NULL,
    `LastSeenAt` datetime(6) NOT NULL,
    `HitCount` int NOT NULL,
    CONSTRAINT `PK_DetectionFingerprints` PRIMARY KEY (`Id`)
) CHARACTER SET=utf8mb4;

CREATE TABLE `DetectionEvents` (
    `Id` varchar(255) CHARACTER SET utf8mb4 NOT NULL,
    `Type` varchar(50) CHARACTER SET utf8mb4 NOT NULL,
    `Severity` varchar(20) CHARACTER SET utf8mb4 NOT NULL,
    `Description` longtext CHARACTER SET utf8mb4 NOT NULL,
    `Confidence` double NOT NULL,
    `EvidencePath` longtext CHARACTER SET utf8mb4 NULL,
    `PlayerId` varchar(255) CHARACTER SET utf8mb4 NULL,
    `ProcessName` longtext CHARACTER SET utf8mb4 NULL,
    `Timestamp` datetime(6) NOT NULL,
    CONSTRAINT `PK_DetectionEvents` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_DetectionEvents_Users_PlayerId` FOREIGN KEY (`PlayerId`) REFERENCES `Users` (`Id`)
) CHARACTER SET=utf8mb4;

CREATE TABLE `PlayerReports` (
    `Id` varchar(255) CHARACTER SET utf8mb4 NOT NULL,
    `PlayerName` varchar(100) CHARACTER SET utf8mb4 NOT NULL,
    `Reason` varchar(200) CHARACTER SET utf8mb4 NOT NULL,
    `Description` longtext CHARACTER SET utf8mb4 NOT NULL,
    `Status` varchar(20) CHARACTER SET utf8mb4 NOT NULL,
    `Result` longtext CHARACTER SET utf8mb4 NULL,
    `ReporterId` varchar(255) CHARACTER SET utf8mb4 NULL,
    `CreatedAt` datetime(6) NOT NULL,
    CONSTRAINT `PK_PlayerReports` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_PlayerReports_Users_ReporterId` FOREIGN KEY (`ReporterId`) REFERENCES `Users` (`Id`)
) CHARACTER SET=utf8mb4;

CREATE INDEX `IX_ActivityEvents_Timestamp` ON `ActivityEvents` (`Timestamp`);

CREATE INDEX `IX_Alerts_Resolved` ON `Alerts` (`Resolved`);

CREATE INDEX `IX_Appeals_Player` ON `Appeals` (`Player`);

CREATE INDEX `IX_Appeals_Status` ON `Appeals` (`Status`);

CREATE INDEX `IX_AuditLogEntries_Action` ON `AuditLogEntries` (`Action`);

CREATE INDEX `IX_AuditLogEntries_Timestamp` ON `AuditLogEntries` (`Timestamp`);

CREATE INDEX `IX_BanEntries_Player` ON `BanEntries` (`Player`);

CREATE INDEX `IX_DetectionFingerprints_Fingerprint` ON `DetectionFingerprints` (`Fingerprint`);
CREATE INDEX `IX_DetectionFingerprints_LastSeenAt` ON `DetectionFingerprints` (`LastSeenAt`);
CREATE INDEX `IX_DetectionFingerprints_Fingerprint_LastSeenAt` ON `DetectionFingerprints` (`Fingerprint`, `LastSeenAt`);

CREATE INDEX `IX_DetectionEvents_PlayerId` ON `DetectionEvents` (`PlayerId`);

CREATE INDEX `IX_DetectionEvents_Timestamp` ON `DetectionEvents` (`Timestamp`);

CREATE UNIQUE INDEX `IX_Devices_DeviceId` ON `Devices` (`DeviceId`);

CREATE INDEX `IX_Devices_UserId` ON `Devices` (`UserId`);

CREATE INDEX `IX_ModeratorReports_CreatedAt` ON `ModeratorReports` (`CreatedAt`);

CREATE INDEX `IX_ModeratorReports_Status` ON `ModeratorReports` (`Status`);

CREATE INDEX `IX_PlayerReports_ReporterId` ON `PlayerReports` (`ReporterId`);

CREATE INDEX `IX_PlayerReports_Status` ON `PlayerReports` (`Status`);

CREATE UNIQUE INDEX `IX_RefreshTokens_Token` ON `RefreshTokens` (`Token`);

CREATE INDEX `IX_RefreshTokens_UserId` ON `RefreshTokens` (`UserId`);

CREATE INDEX `IX_RefreshTokens_UserId_IsRevoked` ON `RefreshTokens` (`UserId`, `IsRevoked`);

CREATE INDEX `IX_Sessions_SessionId_UserId` ON `Sessions` (`SessionId`, `UserId`);

CREATE INDEX `IX_Sessions_UserId` ON `Sessions` (`UserId`);

CREATE INDEX `IX_TimelineEvents_Severity` ON `TimelineEvents` (`Severity`);

CREATE INDEX `IX_TimelineEvents_Timestamp` ON `TimelineEvents` (`Timestamp`);

CREATE INDEX `IX_Users_Status` ON `Users` (`Status`);

CREATE UNIQUE INDEX `IX_Users_Username` ON `Users` (`Username`);

INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
VALUES ('20260628085500_Phase4And5Entities', '8.0.11');

-- Appeal ticket system (Phase 6)
CREATE TABLE `AppealMessages` (
    `Id` varchar(255) CHARACTER SET utf8mb4 NOT NULL,
    `AppealId` varchar(255) CHARACTER SET utf8mb4 NOT NULL,
    `SenderId` varchar(255) CHARACTER SET utf8mb4 NOT NULL,
    `SenderName` varchar(100) CHARACTER SET utf8mb4 NOT NULL,
    `Message` varchar(4000) CHARACTER SET utf8mb4 NOT NULL,
    `CreatedAt` datetime(6) NOT NULL,
    CONSTRAINT `PK_AppealMessages` PRIMARY KEY (`Id`)
) CHARACTER SET=utf8mb4;

CREATE INDEX `IX_AppealMessages_AppealId` ON `AppealMessages` (`AppealId`);
CREATE INDEX `IX_AppealMessages_CreatedAt` ON `AppealMessages` (`CreatedAt`);

ALTER TABLE `Appeals` ADD COLUMN `PlayerId` varchar(255) CHARACTER SET utf8mb4 NULL;
ALTER TABLE `Appeals` ADD COLUMN `BanId` varchar(255) CHARACTER SET utf8mb4 NULL;
CREATE INDEX `IX_Appeals_PlayerId` ON `Appeals` (`PlayerId`);

INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
VALUES ('20260701000000_AddAppealMessages', '8.0.11');

COMMIT;

