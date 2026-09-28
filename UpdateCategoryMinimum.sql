-- Run once against the configured application database before using the new boundary.
SET XACT_ABORT ON;
BEGIN TRANSACTION;
IF EXISTS (SELECT 1 FROM Categories WHERE MinWeightKg = 0)
BEGIN
    IF EXISTS (SELECT 1 FROM Categories WHERE MinWeightKg = 0.01)
        THROW 50001, 'Both 0 and 0.01 boundaries exist. Resolve the categories before migrating.', 1;
    UPDATE Categories SET MinWeightKg = 0.01 WHERE MinWeightKg = 0;
END;
COMMIT TRANSACTION;
