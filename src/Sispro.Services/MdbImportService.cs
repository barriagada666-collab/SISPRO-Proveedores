using System.Globalization;
using Microsoft.Data.Sqlite;
using Sispro.Core.Dtos;
using Sispro.Core.Entities;
using Sispro.Core.Enums;
using Sispro.Core.Interfaces;
using Sispro.Data.Database;

namespace Sispro.Services;

/// <summary>
/// Analiza e importa proveedores (y opcionalmente solicitantes / OTs nuevas) desde una base Microsoft Access (.mdb)
/// hacia la base de datos SQLite activa, calculando diferencias previas antes de aplicar cambios.
/// </summary>
public sealed class MdbImportService(
    SqliteConnectionFactory factory,
    IProviderRepository providers,
    IRequesterRepository requesters,
    IAuditRepository audit,
    IMdbTableReader mdbReader,
    SisproSyncHub? syncHub = null,
    IUserRepository? users = null)
{
    public OperationalDatabaseCounts GetOperationalCounts(UserSession session)
    {
        AuthService.EnsureAdministrator(session, users, audit, "Importación MDB", "Consultar conteos operativos de la base de datos");
        return ReadOperationalCounts();
    }

    public MdbDatabaseCleanResult CleanOperationalDatabase(UserSession session, bool createBackup = true)
    {
        AuthService.EnsureAdministrator(session, users, audit, "Importación MDB", "Limpiar datos operativos de sispro.db para importación limpia");
        OperationalDatabaseCounts before = ReadOperationalCounts();

        string? backupPath = null;
        if (createBackup)
        {
            backupPath = CreateSafetyBackup("sispro_pre_limpieza");
        }

        if (before.ProvidersCount + before.RequestersCount + before.WorkOrdersCount + before.WorkOrderDetailsCount > 0)
        {
            using SqliteConnection conn = factory.OpenConnection();
            using SqliteTransaction tx = conn.BeginTransaction();
            ClearOperationalTables(conn, tx);
            tx.Commit();
        }

        string detail =
            $"Limpieza operativa de sispro.db: -{before.ProvidersCount} proveedores, -{before.RequestersCount} solicitantes, " +
            $"-{before.WorkOrdersCount} OTs (-{before.WorkOrderDetailsCount} ítems). Conservados: Usuarios, Grupos, Activos, Configuración y Auditoría." +
            (backupPath is not null ? $" | Respaldo: {Path.GetFileName(backupPath)}" : string.Empty);

        audit.Log(session.Usuario.AliasUsuario, "Importación MDB", "LIMPIAR_BD_OPERATIVA", detail);
        syncHub?.Publish(DataChangeKind.Catalogs | DataChangeKind.WorkOrders | DataChangeKind.Audit, "LIMPIAR_BD_OPERATIVA");

        return new MdbDatabaseCleanResult
        {
            BackupPath = backupPath,
            ProvidersDeleted = before.ProvidersCount,
            RequestersDeleted = before.RequestersCount,
            WorkOrdersDeleted = before.WorkOrdersCount,
            WorkOrderDetailsDeleted = before.WorkOrderDetailsCount
        };
    }

    public MdbImportPreview AnalyzeMdb(UserSession session, string mdbPath, MdbImportOptions? options = null)
    {
        AuthService.EnsureAdministrator(session, users, audit, "Importación MDB", "Analizar archivo Access (.mdb)");
        (MdbImportPreview preview, _) = BuildAnalysis(mdbPath, options ?? new MdbImportOptions());
        return preview;
    }

    public MdbImportResult ApplyImport(UserSession session, string mdbPath, MdbImportOptions? options = null)
    {
        AuthService.EnsureAdministrator(session, users, audit, "Importación MDB", "Importar y actualizar base de datos desde Access (.mdb)");
        MdbImportOptions effectiveOptions = options ?? new MdbImportOptions();
        bool fullRewrite = effectiveOptions.RewriteAllFromMdb;
        (MdbImportPreview preview, MdbSnapshot snapshot) = BuildAnalysis(mdbPath, effectiveOptions);

        int plannedProvInserts = (fullRewrite || effectiveOptions.InsertNewProviders) ? preview.NewProvidersCount : 0;
        int plannedProvUpdates = (!fullRewrite && effectiveOptions.UpdateExistingProviders) ? preview.UpdatedProvidersCount : 0;
        int plannedReqChanges = effectiveOptions.SyncNewRequesters ? preview.NewOrUpdatedRequesters.Count : 0;
        int plannedOtInserts = effectiveOptions.SyncNewWorkOrders ? preview.NewWorkOrdersCount : 0;

        bool willModifyDatabase = fullRewrite || (plannedProvInserts + plannedProvUpdates + plannedReqChanges + plannedOtInserts > 0);

        string? backupPath = null;
        if (willModifyDatabase && effectiveOptions.CreateBackupBeforeApply)
        {
            backupPath = CreateSafetyBackup(fullRewrite ? "sispro_pre_mdb_rewrite" : "sispro_pre_mdb");
        }

        int clearedProviders = 0;
        int clearedRequesters = 0;
        int clearedWorkOrders = 0;
        int insertedProviders = 0;
        int updatedProviders = 0;
        int insertedRequesters = 0;
        int updatedRequesters = 0;
        int insertedOts = 0;
        int insertedOtDetails = 0;
        List<string> warnings = [.. preview.Warnings];

        Dictionary<string, Provider> existingProvidersByName = fullRewrite
            ? new Dictionary<string, Provider>(StringComparer.OrdinalIgnoreCase)
            : providers
                .GetAll(includeInactive: true)
                .GroupBy(p => Clean(p.Nombre), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        Dictionary<int, int> legacyProvIdToSqlId = new();
        if (!fullRewrite)
        {
            foreach (MdbProviderRow raw in snapshot.Providers)
            {
                string key = Clean(raw.Nombre);
                if (raw.LegacyIdProv > 0 && existingProvidersByName.TryGetValue(key, out Provider? existingProv))
                {
                    legacyProvIdToSqlId[raw.LegacyIdProv] = existingProv.IdProv;
                }
            }
        }

        Dictionary<string, Requester> existingRequestersByAlias = fullRewrite
            ? new Dictionary<string, Requester>(StringComparer.OrdinalIgnoreCase)
            : requesters
                .GetAll(includeInactive: true)
                .GroupBy(r => Clean(r.Alias), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        Dictionary<int, int> legacySolIdToSqlId = new();
        if (!fullRewrite)
        {
            foreach (MdbRequesterRow req in snapshot.Requesters)
            {
                if (req.LegacyIdSol > 0 && existingRequestersByAlias.TryGetValue(Clean(req.Alias), out Requester? existingReq))
                {
                    legacySolIdToSqlId[req.LegacyIdSol] = existingReq.IdSol;
                }
            }
        }

        if (willModifyDatabase)
        {
            using SqliteConnection conn = factory.OpenConnection();
            using SqliteTransaction tx = conn.BeginTransaction();

            // 0. Si se seleccionó reescritura completa, limpiar tablas operativas atómicamente dentro de la misma transacción
            if (fullRewrite)
            {
                clearedProviders = preview.DbProvidersBefore;
                clearedRequesters = preview.DbRequestersBefore;
                clearedWorkOrders = preview.DbWorkOrdersBefore;
                ClearOperationalTables(conn, tx);
            }

            // 1. Proveedores (nuevos + actualización de campos)
            foreach (MdbProviderDiffItem diff in preview.ProviderDiffs)
            {
                if (diff.Action == MdbDiffAction.Nuevo && (fullRewrite || effectiveOptions.InsertNewProviders))
                {
                    using SqliteCommand insertCmd = conn.CreateCommand();
                    insertCmd.Transaction = tx;
                    insertCmd.CommandText = """
                        INSERT INTO Proveedores(nom_prov, dir_prov, fono_prov, fax_prov, mail_prov, activo)
                        VALUES ($nom, $dir, $fono, $fax, $mail, 1);
                        SELECT last_insert_rowid();
                        """;
                    insertCmd.Parameters.AddWithValue("$nom", diff.Nombre);
                    insertCmd.Parameters.AddWithValue("$dir", diff.DireccionFinal);
                    insertCmd.Parameters.AddWithValue("$fono", diff.TelefonoFinal);
                    insertCmd.Parameters.AddWithValue("$fax", diff.FaxFinal);
                    insertCmd.Parameters.AddWithValue("$mail", diff.CorreoFinal);

                    int newProvId = Convert.ToInt32(insertCmd.ExecuteScalar(), CultureInfo.InvariantCulture);
                    insertedProviders++;
                    if (diff.LegacyIdProv > 0)
                    {
                        legacyProvIdToSqlId[diff.LegacyIdProv] = newProvId;
                    }
                    existingProvidersByName[Clean(diff.Nombre)] = new Provider
                    {
                        IdProv = newProvId,
                        Nombre = diff.Nombre,
                        Direccion = diff.DireccionFinal,
                        Telefono = diff.TelefonoFinal,
                        Fax = diff.FaxFinal,
                        Correo = diff.CorreoFinal,
                        Activo = true
                    };
                }
                else if (!fullRewrite &&
                         diff.Action == MdbDiffAction.Actualizar &&
                         effectiveOptions.UpdateExistingProviders &&
                         diff.TargetIdProv.HasValue)
                {
                    using SqliteCommand updCmd = conn.CreateCommand();
                    updCmd.Transaction = tx;
                    updCmd.CommandText = """
                        UPDATE Proveedores
                        SET dir_prov = $dir,
                            fono_prov = $fono,
                            fax_prov = $fax,
                            mail_prov = $mail
                        WHERE id_prov = $id;
                        """;
                    updCmd.Parameters.AddWithValue("$id", diff.TargetIdProv.Value);
                    updCmd.Parameters.AddWithValue("$dir", diff.DireccionFinal);
                    updCmd.Parameters.AddWithValue("$fono", diff.TelefonoFinal);
                    updCmd.Parameters.AddWithValue("$fax", diff.FaxFinal);
                    updCmd.Parameters.AddWithValue("$mail", diff.CorreoFinal);
                    updCmd.ExecuteNonQuery();
                    updatedProviders++;
                }
            }

            // Mapear todos los IDs legacy del MDB (incluidos duplicados consolidados por nombre) al ID SQLite resultante
            foreach (MdbProviderRow raw in snapshot.Providers)
            {
                string key = Clean(raw.Nombre);
                if (raw.LegacyIdProv > 0 && existingProvidersByName.TryGetValue(key, out Provider? mappedProv))
                {
                    legacyProvIdToSqlId[raw.LegacyIdProv] = mappedProv.IdProv;
                }
            }

            // 2. Solicitantes (nuevos / datos complementarios, preservando firma digital en modo incremental)
            if (effectiveOptions.SyncNewRequesters)
            {
                foreach (MdbRequesterRow req in preview.NewOrUpdatedRequesters)
                {
                    string aliasKey = Clean(req.Alias);
                    if (string.IsNullOrWhiteSpace(aliasKey))
                    {
                        continue;
                    }

                    if (!fullRewrite && existingRequestersByAlias.TryGetValue(aliasKey, out Requester? existingReq))
                    {
                        string nom = PickField(req.Nombre, existingReq.Nombre, effectiveOptions.OverwriteWithEmptyValues);
                        string pat = PickField(req.ApellidoPaterno, existingReq.ApellidoPaterno, effectiveOptions.OverwriteWithEmptyValues);
                        string mat = PickField(req.ApellidoMaterno, existingReq.ApellidoMaterno, effectiveOptions.OverwriteWithEmptyValues);
                        string fono = PickField(req.Telefono, existingReq.Telefono, effectiveOptions.OverwriteWithEmptyValues);
                        string mail = PickField(req.Correo, existingReq.Correo, effectiveOptions.OverwriteWithEmptyValues);

                        using SqliteCommand updSol = conn.CreateCommand();
                        updSol.Transaction = tx;
                        updSol.CommandText = """
                            UPDATE Solicitantes
                            SET nombre_sol = $nom,
                                ape_pat_sol = $pat,
                                ape_mat_sol = $mat,
                                fono_sol = $fono,
                                mail_sol = $mail
                            WHERE id_sol = $id;
                            """;
                        updSol.Parameters.AddWithValue("$id", existingReq.IdSol);
                        updSol.Parameters.AddWithValue("$nom", nom);
                        updSol.Parameters.AddWithValue("$pat", string.IsNullOrWhiteSpace(pat) ? aliasKey : pat);
                        updSol.Parameters.AddWithValue("$mat", mat);
                        updSol.Parameters.AddWithValue("$fono", fono);
                        updSol.Parameters.AddWithValue("$mail", mail);
                        updSol.ExecuteNonQuery();
                        updatedRequesters++;
                    }
                    else if (!existingRequestersByAlias.ContainsKey(aliasKey))
                    {
                        string nom = string.IsNullOrWhiteSpace(req.Nombre) ? aliasKey : Clean(req.Nombre);
                        string pat = string.IsNullOrWhiteSpace(req.ApellidoPaterno) ? aliasKey : Clean(req.ApellidoPaterno);

                        using SqliteCommand insSol = conn.CreateCommand();
                        insSol.Transaction = tx;
                        insSol.CommandText = """
                            INSERT INTO Solicitantes(alias, nombre_sol, ape_pat_sol, ape_mat_sol, fono_sol, mail_sol, activo)
                            VALUES ($alias, $nom, $pat, $mat, $fono, $mail, 1);
                            SELECT last_insert_rowid();
                            """;
                        insSol.Parameters.AddWithValue("$alias", aliasKey);
                        insSol.Parameters.AddWithValue("$nom", nom);
                        insSol.Parameters.AddWithValue("$pat", pat);
                        insSol.Parameters.AddWithValue("$mat", Clean(req.ApellidoMaterno));
                        insSol.Parameters.AddWithValue("$fono", Clean(req.Telefono));
                        insSol.Parameters.AddWithValue("$mail", Clean(req.Correo));

                        int newSolId = Convert.ToInt32(insSol.ExecuteScalar(), CultureInfo.InvariantCulture);
                        insertedRequesters++;
                        existingRequestersByAlias[aliasKey] = new Requester
                        {
                            IdSol = newSolId,
                            Alias = aliasKey,
                            Nombre = nom,
                            ApellidoPaterno = pat,
                            ApellidoMaterno = Clean(req.ApellidoMaterno),
                            Telefono = Clean(req.Telefono),
                            Correo = Clean(req.Correo),
                            Activo = true
                        };
                        if (req.LegacyIdSol > 0)
                        {
                            legacySolIdToSqlId[req.LegacyIdSol] = newSolId;
                        }
                    }
                }

                // Mapear todos los IDs legacy de solicitantes (incluidos duplicados consolidados por alias)
                foreach (MdbRequesterRow rawSol in snapshot.Requesters)
                {
                    string aliasKey = Clean(rawSol.Alias);
                    if (rawSol.LegacyIdSol > 0 && existingRequestersByAlias.TryGetValue(aliasKey, out Requester? mappedSol))
                    {
                        legacySolIdToSqlId[rawSol.LegacyIdSol] = mappedSol.IdSol;
                    }
                }
            }

            // 3. Órdenes de trabajo nuevas (opcional)
            if (effectiveOptions.SyncNewWorkOrders && preview.NewWorkOrders.Count > 0)
            {
                Dictionary<int, string> assetNames = LoadAssetNames(conn, tx);
                ILookup<int, MdbWorkOrderDetailRow> detailsByLegacyOt = preview.NewWorkOrderDetails.ToLookup(d => d.LegacyIdOt);

                foreach (MdbWorkOrderRow ot in preview.NewWorkOrders)
                {
                    if (!legacyProvIdToSqlId.TryGetValue(ot.LegacyIdProv, out int targetIdProv))
                    {
                        warnings.Add($"OT N° {ot.NumeroOt} omitida: proveedor MDB ID {ot.LegacyIdProv} no fue importado (active 'Importar nuevos proveedores').");
                        continue;
                    }

                    object targetIdSol = DBNull.Value;
                    if (ot.LegacyIdSol.HasValue && legacySolIdToSqlId.TryGetValue(ot.LegacyIdSol.Value, out int sqlSol))
                    {
                        targetIdSol = sqlSol;
                    }

                    string assetRoute = BuildAssetRoute(ot, assetNames);

                    using SqliteCommand insOt = conn.CreateCommand();
                    insOt.Transaction = tx;
                    insOt.CommandText = """
                        INSERT INTO OrdenesTrabajo(
                            n_ot, id_prov, id_sol, fecha_ot, estado, guia_des_ot,
                            id_n1, id_n2, id_n3, id_n4, id_n5,
                            id_n6, id_n7, id_n8, id_n9, id_n10,
                            id_nivel, ruta_activo, observacion, valor_neto, firmas_mask
                        ) VALUES (
                            $not, $prov, $sol, $fecha, $estado, $guia,
                            $n1, $n2, $n3, $n4, $n5,
                            $n6, $n7, $n8, $n9, $n10,
                            $nivel, $ruta, '', 0, 7
                        );
                        SELECT last_insert_rowid();
                        """;
                    insOt.Parameters.AddWithValue("$not", ot.NumeroOt);
                    insOt.Parameters.AddWithValue("$prov", targetIdProv);
                    insOt.Parameters.AddWithValue("$sol", targetIdSol);
                    insOt.Parameters.AddWithValue("$fecha", ot.FechaOt.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
                    insOt.Parameters.AddWithValue("$estado", ot.ActivoFlag ? (int)WorkOrderStatus.Terminada : (int)WorkOrderStatus.Abierta);
                    insOt.Parameters.AddWithValue("$guia", ot.GuiaDespacho);
                    insOt.Parameters.AddWithValue("$n1", ot.IdN1);
                    insOt.Parameters.AddWithValue("$n2", ot.IdN2);
                    insOt.Parameters.AddWithValue("$n3", ot.IdN3);
                    insOt.Parameters.AddWithValue("$n4", ot.IdN4);
                    insOt.Parameters.AddWithValue("$n5", ot.IdN5);
                    insOt.Parameters.AddWithValue("$n6", ot.IdN6);
                    insOt.Parameters.AddWithValue("$n7", ot.IdN7);
                    insOt.Parameters.AddWithValue("$n8", ot.IdN8);
                    insOt.Parameters.AddWithValue("$n9", ot.IdN9);
                    insOt.Parameters.AddWithValue("$n10", ot.IdN10);
                    insOt.Parameters.AddWithValue("$nivel", Math.Clamp(ot.IdNivel, 1, 10));
                    insOt.Parameters.AddWithValue("$ruta", assetRoute);

                    int newIdOt = Convert.ToInt32(insOt.ExecuteScalar(), CultureInfo.InvariantCulture);
                    insertedOts++;

                    foreach (MdbWorkOrderDetailRow det in detailsByLegacyOt[ot.LegacyIdOt])
                    {
                        using SqliteCommand insDet = conn.CreateCommand();
                        insDet.Transaction = tx;
                        insDet.CommandText = """
                            INSERT INTO DetalleOT(id_ot, cantidad_det, detalle_ot, estado_det)
                            VALUES ($idot, $cant, $det, $est);
                            """;
                        insDet.Parameters.AddWithValue("$idot", newIdOt);
                        insDet.Parameters.AddWithValue("$cant", Math.Max(1, (int)Math.Round(det.Cantidad)));
                        insDet.Parameters.AddWithValue("$det", Clean(det.Detalle));
                        insDet.Parameters.AddWithValue("$est", det.EstadoDet ? 1 : 0);
                        insDet.ExecuteNonQuery();
                        insertedOtDetails++;
                    }
                }
            }

            tx.Commit();
        }

        int dbProvidersAfter = providers.GetAll(includeInactive: true).Count;
        int dbWorkOrdersAfter = CountWorkOrders();

        if (willModifyDatabase)
        {
            string actionCode = fullRewrite ? "REESCRIBIR_BD_DESDE_MDB" : "IMPORTAR_MDB";
            string auditDetail =
                (fullRewrite ? $"[REESCRITURA COMPLETA: -{clearedProviders} prov., -{clearedRequesters} sol., -{clearedWorkOrders} OTs] " : string.Empty) +
                $"Archivo: {Path.GetFileName(preview.MdbPath)} | " +
                $"Proveedores (+{insertedProviders} nuevos, {updatedProviders} actualizados) | " +
                $"Solicitantes (+{insertedRequesters}, {updatedRequesters} act.) | " +
                $"OTs (+{insertedOts}, ítems +{insertedOtDetails})" +
                (backupPath is not null ? $" | Respaldo: {Path.GetFileName(backupPath)}" : string.Empty);

            audit.Log(session.Usuario.AliasUsuario, "Importación MDB", actionCode, auditDetail);

            DataChangeKind changes = DataChangeKind.Catalogs | DataChangeKind.Audit;
            if (fullRewrite || insertedOts > 0)
            {
                changes |= DataChangeKind.WorkOrders;
            }

            syncHub?.Publish(changes, actionCode);
        }

        return new MdbImportResult
        {
            MdbPath = preview.MdbPath,
            BackupPath = backupPath,
            WasFullRewrite = fullRewrite,
            ProvidersCleared = clearedProviders,
            RequestersCleared = clearedRequesters,
            WorkOrdersCleared = clearedWorkOrders,
            ProvidersInserted = insertedProviders,
            ProvidersUpdated = updatedProviders,
            ProvidersUnchanged = fullRewrite
                ? 0
                : preview.UnchangedProvidersCount +
                  (effectiveOptions.InsertNewProviders ? 0 : preview.NewProvidersCount) +
                  (effectiveOptions.UpdateExistingProviders ? 0 : preview.UpdatedProvidersCount),
            RequestersInserted = insertedRequesters,
            RequestersUpdated = updatedRequesters,
            WorkOrdersInserted = insertedOts,
            WorkOrderDetailsInserted = insertedOtDetails,
            DbProvidersAfter = dbProvidersAfter,
            DbWorkOrdersAfter = dbWorkOrdersAfter,
            Warnings = warnings
        };
    }

    private (MdbImportPreview Preview, MdbSnapshot Snapshot) BuildAnalysis(string mdbPath, MdbImportOptions options)
    {
        MdbSnapshot snapshot = mdbReader.ReadSnapshot(mdbPath);
        List<string> warnings = [];
        bool fullRewrite = options.RewriteAllFromMdb;

        OperationalDatabaseCounts dbCounts = ReadOperationalCounts();
        IReadOnlyList<Provider> dbProviders = providers.GetAll(includeInactive: true);
        Dictionary<string, Provider> dbByName = new(StringComparer.OrdinalIgnoreCase);
        if (!fullRewrite)
        {
            foreach (Provider p in dbProviders)
            {
                string key = Clean(p.Nombre);
                dbByName.TryAdd(key, p);
            }
        }
        else
        {
            warnings.Add(
                $"MODO REESCRITURA COMPLETA: se limpiarán {dbCounts.ProvidersCount} proveedores, " +
                $"{dbCounts.RequestersCount} solicitantes y {dbCounts.WorkOrdersCount} OTs actuales antes de importar desde cero el contenido del MDB.");
        }

        // Deduplicar nombres repetidos dentro del mismo MDB (conservando datos no vacíos)
        Dictionary<string, MdbProviderRow> uniqueMdbProviders = new(StringComparer.OrdinalIgnoreCase);
        int duplicateProvInMdb = 0;
        foreach (MdbProviderRow raw in snapshot.Providers)
        {
            string key = Clean(raw.Nombre);
            if (string.IsNullOrWhiteSpace(key))
            {
                continue;
            }

            if (uniqueMdbProviders.TryGetValue(key, out MdbProviderRow? prev))
            {
                duplicateProvInMdb++;
                uniqueMdbProviders[key] = new MdbProviderRow
                {
                    LegacyIdProv = prev.LegacyIdProv > 0 ? prev.LegacyIdProv : raw.LegacyIdProv,
                    Nombre = key,
                    Direccion = string.IsNullOrWhiteSpace(raw.Direccion) ? prev.Direccion : Clean(raw.Direccion),
                    Telefono = string.IsNullOrWhiteSpace(raw.Telefono) ? prev.Telefono : Clean(raw.Telefono),
                    Fax = string.IsNullOrWhiteSpace(raw.Fax) ? prev.Fax : Clean(raw.Fax),
                    Correo = string.IsNullOrWhiteSpace(raw.Correo) ? prev.Correo : Clean(raw.Correo)
                };
            }
            else
            {
                uniqueMdbProviders[key] = new MdbProviderRow
                {
                    LegacyIdProv = raw.LegacyIdProv,
                    Nombre = key,
                    Direccion = Clean(raw.Direccion),
                    Telefono = Clean(raw.Telefono),
                    Fax = Clean(raw.Fax),
                    Correo = Clean(raw.Correo)
                };
            }
        }

        if (duplicateProvInMdb > 0)
        {
            warnings.Add($"Se detectaron y consolidaron {duplicateProvInMdb} registro(s) de proveedores duplicados por nombre en el archivo MDB.");
        }

        List<MdbProviderDiffItem> diffs = [];
        int newCount = 0;
        int updCount = 0;
        int unchangedCount = 0;

        foreach (MdbProviderRow row in uniqueMdbProviders.Values)
        {
            if (dbByName.TryGetValue(row.Nombre, out Provider? existing))
            {
                string curDir = Clean(existing.Direccion);
                string curFono = Clean(existing.Telefono);
                string curFax = Clean(existing.Fax);
                string curMail = Clean(existing.Correo);

                string finDir = PickField(row.Direccion, curDir, options.OverwriteWithEmptyValues);
                string finFono = PickField(row.Telefono, curFono, options.OverwriteWithEmptyValues);
                string finFax = PickField(row.Fax, curFax, options.OverwriteWithEmptyValues);
                string finMail = PickField(row.Correo, curMail, options.OverwriteWithEmptyValues);

                List<string> changedFields = [];
                List<string> previewNotes = [];

                CheckFieldDiff("Dirección", curDir, finDir, changedFields, previewNotes);
                CheckFieldDiff("Teléfono", curFono, finFono, changedFields, previewNotes);
                CheckFieldDiff("Fax", curFax, finFax, changedFields, previewNotes);
                CheckFieldDiff("Correo", curMail, finMail, changedFields, previewNotes);

                MdbDiffAction action = changedFields.Count > 0 ? MdbDiffAction.Actualizar : MdbDiffAction.SinCambios;
                if (action == MdbDiffAction.Actualizar) updCount++; else unchangedCount++;

                diffs.Add(new MdbProviderDiffItem
                {
                    Action = action,
                    TargetIdProv = existing.IdProv,
                    LegacyIdProv = row.LegacyIdProv,
                    Nombre = row.Nombre,
                    DireccionMdb = row.Direccion,
                    TelefonoMdb = row.Telefono,
                    FaxMdb = row.Fax,
                    CorreoMdb = row.Correo,
                    DireccionActual = curDir,
                    TelefonoActual = curFono,
                    FaxActual = curFax,
                    CorreoActual = curMail,
                    DireccionFinal = finDir,
                    TelefonoFinal = finFono,
                    FaxFinal = finFax,
                    CorreoFinal = finMail,
                    ChangedFields = changedFields,
                    PreviewBeforeAfter = previewNotes.Count == 0 ? "Datos idénticos en BD actual" : string.Join(" | ", previewNotes)
                });
            }
            else
            {
                newCount++;
                diffs.Add(new MdbProviderDiffItem
                {
                    Action = MdbDiffAction.Nuevo,
                    TargetIdProv = null,
                    LegacyIdProv = row.LegacyIdProv,
                    Nombre = row.Nombre,
                    DireccionMdb = row.Direccion,
                    TelefonoMdb = row.Telefono,
                    FaxMdb = row.Fax,
                    CorreoMdb = row.Correo,
                    DireccionFinal = row.Direccion,
                    TelefonoFinal = row.Telefono,
                    FaxFinal = row.Fax,
                    CorreoFinal = row.Correo,
                    ChangedFields = [fullRewrite ? "Reescritura completa" : "Registro completo"],
                    PreviewBeforeAfter = fullRewrite
                        ? $"Se importará desde cero ({FormatCompactContact(row)})"
                        : $"Se agregará nuevo proveedor ({FormatCompactContact(row)})"
                });
            }
        }

        diffs = diffs
            .OrderBy(d => d.Action)
            .ThenBy(d => d.Nombre, StringComparer.OrdinalIgnoreCase)
            .ToList();

        // Deduplicar solicitantes por alias dentro del mismo MDB para evitar colisiones UNIQUE
        Dictionary<string, MdbRequesterRow> uniqueMdbRequesters = new(StringComparer.OrdinalIgnoreCase);
        foreach (MdbRequesterRow rawReq in snapshot.Requesters)
        {
            string aliasKey = Clean(rawReq.Alias);
            if (string.IsNullOrWhiteSpace(aliasKey))
            {
                continue;
            }

            if (uniqueMdbRequesters.TryGetValue(aliasKey, out MdbRequesterRow? prevReq))
            {
                uniqueMdbRequesters[aliasKey] = new MdbRequesterRow
                {
                    LegacyIdSol = prevReq.LegacyIdSol > 0 ? prevReq.LegacyIdSol : rawReq.LegacyIdSol,
                    Alias = prevReq.Alias,
                    Nombre = string.IsNullOrWhiteSpace(rawReq.Nombre) ? prevReq.Nombre : Clean(rawReq.Nombre),
                    ApellidoPaterno = string.IsNullOrWhiteSpace(rawReq.ApellidoPaterno) ? prevReq.ApellidoPaterno : Clean(rawReq.ApellidoPaterno),
                    ApellidoMaterno = string.IsNullOrWhiteSpace(rawReq.ApellidoMaterno) ? prevReq.ApellidoMaterno : Clean(rawReq.ApellidoMaterno),
                    Telefono = string.IsNullOrWhiteSpace(rawReq.Telefono) ? prevReq.Telefono : Clean(rawReq.Telefono),
                    Correo = string.IsNullOrWhiteSpace(rawReq.Correo) ? prevReq.Correo : Clean(rawReq.Correo)
                };
            }
            else
            {
                uniqueMdbRequesters[aliasKey] = new MdbRequesterRow
                {
                    LegacyIdSol = rawReq.LegacyIdSol,
                    Alias = aliasKey,
                    Nombre = Clean(rawReq.Nombre),
                    ApellidoPaterno = Clean(rawReq.ApellidoPaterno),
                    ApellidoMaterno = Clean(rawReq.ApellidoMaterno),
                    Telefono = Clean(rawReq.Telefono),
                    Correo = Clean(rawReq.Correo)
                };
            }
        }

        // Solicitantes comparison
        Dictionary<string, Requester> dbRequestersByAlias = fullRewrite
            ? new Dictionary<string, Requester>(StringComparer.OrdinalIgnoreCase)
            : requesters
                .GetAll(includeInactive: true)
                .GroupBy(r => Clean(r.Alias), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        int newReqCount = 0;
        int updReqCount = 0;
        List<MdbRequesterRow> changedRequesters = [];
        foreach (MdbRequesterRow r in uniqueMdbRequesters.Values)
        {
            string aliasKey = Clean(r.Alias);
            if (!fullRewrite && dbRequestersByAlias.TryGetValue(aliasKey, out Requester? existingReq))
            {
                string finNom = PickField(r.Nombre, Clean(existingReq.Nombre), options.OverwriteWithEmptyValues);
                string finPat = PickField(r.ApellidoPaterno, Clean(existingReq.ApellidoPaterno), options.OverwriteWithEmptyValues);
                string finMat = PickField(r.ApellidoMaterno, Clean(existingReq.ApellidoMaterno), options.OverwriteWithEmptyValues);
                string finFono = PickField(r.Telefono, Clean(existingReq.Telefono), options.OverwriteWithEmptyValues);
                string finMail = PickField(r.Correo, Clean(existingReq.Correo), options.OverwriteWithEmptyValues);

                if (!string.Equals(finNom, Clean(existingReq.Nombre), StringComparison.Ordinal) ||
                    !string.Equals(finPat, Clean(existingReq.ApellidoPaterno), StringComparison.Ordinal) ||
                    !string.Equals(finMat, Clean(existingReq.ApellidoMaterno), StringComparison.Ordinal) ||
                    !string.Equals(finFono, Clean(existingReq.Telefono), StringComparison.Ordinal) ||
                    !string.Equals(finMail, Clean(existingReq.Correo), StringComparison.Ordinal))
                {
                    updReqCount++;
                    changedRequesters.Add(r);
                }
            }
            else
            {
                newReqCount++;
                changedRequesters.Add(r);
            }
        }

        // WorkOrders comparison (por n_ot único en modo incremental; todas en modo reescritura completa)
        HashSet<int> existingOtNumbers = fullRewrite ? [] : LoadExistingOtNumbers();
        List<MdbWorkOrderRow> newWorkOrders = snapshot.WorkOrders
            .Where(ot => fullRewrite || !existingOtNumbers.Contains(ot.NumeroOt))
            .OrderBy(ot => ot.FechaOt)
            .ThenBy(ot => ot.NumeroOt)
            .ToList();

        HashSet<int> newLegacyOtIds = newWorkOrders.Select(ot => ot.LegacyIdOt).ToHashSet();
        List<MdbWorkOrderDetailRow> newDetails = snapshot.WorkOrderDetails
            .Where(d => newLegacyOtIds.Contains(d.LegacyIdOt))
            .ToList();

        MdbImportPreview preview = new()
        {
            MdbPath = snapshot.MdbPath,
            MdbFileSizeBytes = snapshot.FileSizeBytes,
            IsFullRewriteMode = fullRewrite,
            MdbProvidersTotal = uniqueMdbProviders.Count,
            DbProvidersBefore = dbCounts.ProvidersCount,
            DbRequestersBefore = dbCounts.RequestersCount,
            DbWorkOrdersBefore = dbCounts.WorkOrdersCount,
            NewProvidersCount = newCount,
            UpdatedProvidersCount = updCount,
            UnchangedProvidersCount = unchangedCount,
            MdbRequestersTotal = uniqueMdbRequesters.Count,
            NewRequestersCount = newReqCount,
            UpdatedRequestersCount = updReqCount,
            MdbWorkOrdersTotal = snapshot.WorkOrders.Count,
            NewWorkOrdersCount = newWorkOrders.Count,
            NewWorkOrderDetailsCount = newDetails.Count,
            ProviderDiffs = diffs,
            NewOrUpdatedRequesters = changedRequesters,
            NewWorkOrders = newWorkOrders,
            NewWorkOrderDetails = newDetails,
            Warnings = warnings
        };

        return (preview, snapshot);
    }

    private string CreateSafetyBackup(string prefix)
    {
        string dbDir = Path.GetDirectoryName(factory.DatabasePath) ?? AppContext.BaseDirectory;
        string backupDir = Path.Combine(dbDir, "backups");
        Directory.CreateDirectory(backupDir);
        string backupPath = Path.Combine(backupDir, $"{prefix}_{DateTime.Now:yyyyMMdd_HHmmss}.db");
        factory.BackupTo(backupPath);
        return backupPath;
    }

    private OperationalDatabaseCounts ReadOperationalCounts()
    {
        using SqliteConnection conn = factory.OpenConnection();
        int prov = ExecuteScalarCount(conn, "SELECT COUNT(*) FROM Proveedores;");
        int sol = ExecuteScalarCount(conn, "SELECT COUNT(*) FROM Solicitantes;");
        int ot = ExecuteScalarCount(conn, "SELECT COUNT(*) FROM OrdenesTrabajo;");
        int det = ExecuteScalarCount(conn, "SELECT COUNT(*) FROM DetalleOT;");
        return new OperationalDatabaseCounts(prov, sol, ot, det);
    }

    private static int ExecuteScalarCount(SqliteConnection conn, string sql)
    {
        using SqliteCommand cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        return Convert.ToInt32(cmd.ExecuteScalar(), CultureInfo.InvariantCulture);
    }

    private static void ClearOperationalTables(SqliteConnection conn, SqliteTransaction tx)
    {
        string[] statements =
        [
            "DELETE FROM VistoBueno;",
            "DELETE FROM DetalleOT;",
            "DELETE FROM OrdenesTrabajo;",
            "DELETE FROM Solicitantes;",
            "DELETE FROM Proveedores;"
        ];

        foreach (string sql in statements)
        {
            using SqliteCommand cmd = conn.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = sql;
            cmd.ExecuteNonQuery();
        }

        // Reiniciar autoincrement de las tablas operativas si existe sqlite_sequence
        using SqliteCommand chkSeq = conn.CreateCommand();
        chkSeq.Transaction = tx;
        chkSeq.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = 'sqlite_sequence';";
        if (Convert.ToInt64(chkSeq.ExecuteScalar(), CultureInfo.InvariantCulture) > 0)
        {
            using SqliteCommand resetSeq = conn.CreateCommand();
            resetSeq.Transaction = tx;
            resetSeq.CommandText = "DELETE FROM sqlite_sequence WHERE name IN ('VistoBueno', 'DetalleOT', 'OrdenesTrabajo', 'Solicitantes', 'Proveedores');";
            resetSeq.ExecuteNonQuery();
        }
    }

    private HashSet<int> LoadExistingOtNumbers()
    {
        HashSet<int> set = [];
        using SqliteConnection conn = factory.OpenConnection();
        using SqliteCommand cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT DISTINCT n_ot FROM OrdenesTrabajo;";
        using SqliteDataReader rdr = cmd.ExecuteReader();
        while (rdr.Read())
        {
            set.Add(rdr.GetInt32(0));
        }

        return set;
    }

    private int CountWorkOrders()
    {
        using SqliteConnection conn = factory.OpenConnection();
        using SqliteCommand cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM OrdenesTrabajo;";
        return Convert.ToInt32(cmd.ExecuteScalar(), CultureInfo.InvariantCulture);
    }

    private static Dictionary<int, string> LoadAssetNames(SqliteConnection conn, SqliteTransaction tx)
    {
        Dictionary<int, string> names = new();
        using SqliteCommand cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = "SELECT id_activo, descripcion FROM Activos;";
        using SqliteDataReader rdr = cmd.ExecuteReader();
        while (rdr.Read())
        {
            names[rdr.GetInt32(0)] = rdr.GetString(1);
        }

        return names;
    }

    private static string BuildAssetRoute(MdbWorkOrderRow ot, Dictionary<int, string> assetNames)
    {
        int[] ids = [ot.IdN1, ot.IdN2, ot.IdN3, ot.IdN4, ot.IdN5, ot.IdN6, ot.IdN7, ot.IdN8, ot.IdN9, ot.IdN10];
        List<string> segments = [];
        foreach (int id in ids)
        {
            if (id > 0 && assetNames.TryGetValue(id, out string? name) && !string.IsNullOrWhiteSpace(name))
            {
                segments.Add(name.Trim());
            }
        }

        return segments.Count > 0 ? string.Join(" \\ ", segments) : string.Empty;
    }

    private static string Clean(string? value) =>
        string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();

    private static string PickField(string mdbValue, string currentValue, bool overwriteWithEmpty)
    {
        string cleanMdb = Clean(mdbValue);
        if (overwriteWithEmpty)
        {
            return cleanMdb;
        }

        return string.IsNullOrWhiteSpace(cleanMdb) ? Clean(currentValue) : cleanMdb;
    }

    private static void CheckFieldDiff(
        string fieldLabel,
        string currentValue,
        string finalValue,
        List<string> changedFields,
        List<string> previewNotes)
    {
        if (string.Equals(currentValue, finalValue, StringComparison.Ordinal))
        {
            return;
        }

        changedFields.Add(fieldLabel);
        string from = string.IsNullOrEmpty(currentValue) ? "(vacío)" : currentValue;
        string to = string.IsNullOrEmpty(finalValue) ? "(vacío)" : finalValue;
        previewNotes.Add($"{fieldLabel}: {from} -> {to}");
    }

    private static string FormatCompactContact(MdbProviderRow row)
    {
        List<string> parts = [];
        if (!string.IsNullOrWhiteSpace(row.Telefono)) parts.Add($"Tel: {row.Telefono}");
        if (!string.IsNullOrWhiteSpace(row.Correo)) parts.Add(row.Correo);
        if (!string.IsNullOrWhiteSpace(row.Direccion)) parts.Add(row.Direccion);
        return parts.Count == 0 ? "solo nombre" : string.Join(" · ", parts);
    }
}
