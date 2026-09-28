/*
   Migración idempotente para Azure SQL.
   Aplicar únicamente después de revisar y respaldar la base de datos.
   El script agrega el estado de Stripe y elimina los campos antiguos de tarjeta.
*/
BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928000000_AddStripeCheckoutState'
)
BEGIN
    DECLARE @var nvarchar(max);
    SELECT @var = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c]
        ON [d].[parent_column_id] = [c].[column_id]
        AND [d].[parent_object_id] = [c].[object_id]
    WHERE [d].[parent_object_id] = OBJECT_ID(N'[Companias]')
      AND [c].[name] = N'CVC';
    IF @var IS NOT NULL EXEC(N'ALTER TABLE [Companias] DROP CONSTRAINT ' + @var + ';');
    ALTER TABLE [Companias] DROP COLUMN [CVC];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928000000_AddStripeCheckoutState'
)
BEGIN
    DECLARE @var1 nvarchar(max);
    SELECT @var1 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c]
        ON [d].[parent_column_id] = [c].[column_id]
        AND [d].[parent_object_id] = [c].[object_id]
    WHERE [d].[parent_object_id] = OBJECT_ID(N'[Companias]')
      AND [c].[name] = N'FechaExpiracion';
    IF @var1 IS NOT NULL EXEC(N'ALTER TABLE [Companias] DROP CONSTRAINT ' + @var1 + ';');
    ALTER TABLE [Companias] DROP COLUMN [FechaExpiracion];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928000000_AddStripeCheckoutState'
)
BEGIN
    DECLARE @var2 nvarchar(max);
    SELECT @var2 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c]
        ON [d].[parent_column_id] = [c].[column_id]
        AND [d].[parent_object_id] = [c].[object_id]
    WHERE [d].[parent_object_id] = OBJECT_ID(N'[Companias]')
      AND [c].[name] = N'NombreTitular';
    IF @var2 IS NOT NULL EXEC(N'ALTER TABLE [Companias] DROP CONSTRAINT ' + @var2 + ';');
    ALTER TABLE [Companias] DROP COLUMN [NombreTitular];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928000000_AddStripeCheckoutState'
)
BEGIN
    DECLARE @var3 nvarchar(max);
    SELECT @var3 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c]
        ON [d].[parent_column_id] = [c].[column_id]
        AND [d].[parent_object_id] = [c].[object_id]
    WHERE [d].[parent_object_id] = OBJECT_ID(N'[Companias]')
      AND [c].[name] = N'NumeroTarjeta';
    IF @var3 IS NOT NULL EXEC(N'ALTER TABLE [Companias] DROP CONSTRAINT ' + @var3 + ';');
    ALTER TABLE [Companias] DROP COLUMN [NumeroTarjeta];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928000000_AddStripeCheckoutState'
)
BEGIN
    ALTER TABLE [Companias] ADD [StripeCheckoutSessionId] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928000000_AddStripeCheckoutState'
)
BEGIN
    ALTER TABLE [Companias] ADD [StripeCustomerId] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928000000_AddStripeCheckoutState'
)
BEGIN
    ALTER TABLE [Companias]
        ADD [StripePaymentStatus] nvarchar(max) NOT NULL DEFAULT N'pending';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928000000_AddStripeCheckoutState'
)
BEGIN
    ALTER TABLE [Companias] ADD [StripeSubscriptionId] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928000000_AddStripeCheckoutState'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260928000000_AddStripeCheckoutState', N'10.0.9');
END;

COMMIT;
GO
