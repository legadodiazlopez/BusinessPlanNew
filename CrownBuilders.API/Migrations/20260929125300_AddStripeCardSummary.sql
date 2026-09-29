BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929125300_AddStripeCardSummary'
)
BEGIN
    ALTER TABLE [Companias] ADD [StripeCardBrand] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929125300_AddStripeCardSummary'
)
BEGIN
    ALTER TABLE [Companias] ADD [StripeCardExpMonth] int NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929125300_AddStripeCardSummary'
)
BEGIN
    ALTER TABLE [Companias] ADD [StripeCardExpYear] int NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929125300_AddStripeCardSummary'
)
BEGIN
    ALTER TABLE [Companias] ADD [StripeCardLast4] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929125300_AddStripeCardSummary'
)
BEGIN
    ALTER TABLE [Companias] ADD [StripeCardholderName] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929125300_AddStripeCardSummary'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260929125300_AddStripeCardSummary', N'10.0.9');
END;

COMMIT;
