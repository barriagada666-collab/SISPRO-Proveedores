using Sispro.Core.Dtos;
using Sispro.Core.Entities;
using Sispro.Core.Enums;
using Sispro.Core.Interfaces;
using Sispro.Core.Security;
using Sispro.Core.Validation;

namespace Sispro.Services;

public sealed class UserManagementService(
    IUserRepository users,
    IRequesterRepository requesters,
    IAuditRepository audit,
    IGroupRepository? groups = null,
    SisproSyncHub? syncHub = null)
{
    public void EnsureCanAccessModule(UserSession? session) =>
        EnsureAdministrator(session, "Acceder al módulo Gestión de Usuarios");

    public IReadOnlyList<User> GetAll(UserSession session, string? search = null) =>
        Search(session, new UserSearchFilter { TextoBusqueda = search });

    public IReadOnlyList<User> Search(UserSession session, UserSearchFilter? filter = null)
    {
        EnsureAdministrator(session, "Consultar usuarios");
        filter ??= new UserSearchFilter();

        string? dateErr = DomainValidator.ValidateDateRange(filter.FechaCreacionInicio, filter.FechaCreacionFin);
        if (dateErr is not null)
        {
            throw new ArgumentException(dateErr);
        }

        return users.Search(filter);
    }

    public User? GetById(UserSession session, string idUsuario)
    {
        EnsureAdministrator(session, "Consultar detalle de usuario");
        return users.GetById(idUsuario);
    }

    public int CountWithoutGroup(UserSession session)
    {
        EnsureAdministrator(session, "Consultar usuarios sin grupo");
        return users.CountWithoutGroup();
    }

    public int CreateUser(UserSession session, User user, string plainPassword, string confirmPassword)
    {
        EnsureAdministrator(session, "Crear usuario");

        if (!string.IsNullOrWhiteSpace(user.IdUsuario))
        {
            string normalizedRut = RutValidator.Normalize(user.IdUsuario);
            if (!RutValidator.IsValid(normalizedRut))
            {
                throw new ArgumentException("El RUT ingresado no es válido (verifique cuerpo y dígito verificador).");
            }

            user.IdUsuario = normalizedRut;
            if (users.GetById(user.IdUsuario) is not null)
            {
                throw new InvalidOperationException($"Ya existe un usuario registrado con el identificador {RutValidator.Format(user.IdUsuario)}.");
            }
        }
        else
        {
            user.IdUsuario = string.Empty;
        }

        ValidateCommonUserFields(user);
        UserGroup? targetGroup = ResolveAndValidateGroup(user.IdGrupo, requireActive: true);

        string? pwdError = DomainValidator.ValidatePasswordPolicy(plainPassword, confirmPassword);
        if (pwdError is not null)
        {
            throw new ArgumentException(pwdError);
        }

        if (users.GetByAlias(user.AliasUsuario) is not null)
        {
            throw new InvalidOperationException($"El nombre de usuario '{user.AliasUsuario.Trim()}' ya se encuentra registrado.");
        }

        user.AliasUsuario = user.AliasUsuario.Trim();
        user.NombreUsuario = user.NombreUsuario.Trim();
        user.ApePatUsuario = user.ApePatUsuario.Trim();
        user.ApeMatUsuario = user.ApeMatUsuario.Trim();
        user.MailUsuario = user.MailUsuario.Trim();
        user.FonoUsuario = user.FonoUsuario.Trim();
        user.NombreGrupo = targetGroup?.Nombre ?? string.Empty;
        user.PassHash = PasswordSecurity.HashPassword(plainPassword);

        EnforceRolePermissionBoundary(user);

        int codigoId = users.Create(user);
        if (user.EsSolicitante)
        {
            requesters.EnsureFromUser(user);
        }

        if (targetGroup is not null && groups is not null)
        {
            groups.RecordMovement(new GroupMovementEntry
            {
                IdUsuario = user.IdUsuario,
                CodigoUsuario = codigoId,
                AliasUsuario = user.AliasUsuario,
                IdGrupoOrigen = null,
                NombreGrupoOrigen = "Sin grupo",
                IdGrupoDestino = targetGroup.IdGrupo,
                NombreGrupoDestino = targetGroup.Nombre,
                EjecutadoPor = session.Usuario.AliasUsuario,
                Motivo = "Asignación inicial al crear usuario"
            });
        }

        audit.Log(
            session.Usuario.AliasUsuario,
            "Usuarios",
            "CREAR_USUARIO",
            $"Usuario '{user.AliasUsuario}' (ID {codigoId}, Nombre '{user.NombreCompleto}', Perfil {user.Nivel}, Grupo '{(targetGroup?.Nombre ?? "Sin grupo")}', Estado {(user.Activo ? "Activo" : "Inactivo")}) creado.");

        syncHub?.Publish(DataChangeKind.Users | DataChangeKind.Groups | DataChangeKind.Audit, "CREAR_USUARIO");
        return codigoId;
    }

    public void UpdateUser(
        UserSession session,
        User user,
        string? newPassword = null,
        string? confirmPassword = null,
        int? expectedRowVersion = null)
    {
        EnsureAdministrator(session, "Modificar usuario");

        User existing = users.GetById(user.IdUsuario)
            ?? throw new InvalidOperationException("El usuario seleccionado no existe.");

        ValidateCommonUserFields(user);
        bool groupChanged = existing.IdGrupo != user.IdGrupo;
        UserGroup? targetGroup = ResolveAndValidateGroup(user.IdGrupo, requireActive: groupChanged);

        string trimmedAlias = user.AliasUsuario.Trim();
        User? aliasConflict = users.GetByAlias(trimmedAlias);
        if (aliasConflict is not null && !string.Equals(aliasConflict.IdUsuario, existing.IdUsuario, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"El nombre de usuario '{trimmedAlias}' ya pertenece a otro usuario.");
        }

        bool isSelf = string.Equals(session.Usuario.IdUsuario, existing.IdUsuario, StringComparison.OrdinalIgnoreCase);
        if (isSelf && !user.Activo)
        {
            throw new InvalidOperationException("No puede desactivar su propia cuenta mientras mantiene la sesión activa.");
        }

        if (isSelf && user.Nivel != UserRoleLevel.Administrador)
        {
            throw new InvalidOperationException("No puede quitarse el perfil Administrador a su propia cuenta.");
        }

        bool updatePwd = !string.IsNullOrEmpty(newPassword) || !string.IsNullOrEmpty(confirmPassword);
        if (updatePwd)
        {
            string? pwdError = DomainValidator.ValidatePasswordPolicy(newPassword, confirmPassword);
            if (pwdError is not null)
            {
                throw new ArgumentException(pwdError);
            }

            user.PassHash = PasswordSecurity.HashPassword(newPassword!);
        }
        else
        {
            user.PassHash = existing.PassHash;
            user.DebeCambiarClave = existing.DebeCambiarClave;
        }

        // Garantizar que siempre exista al menos un Administrador activo
        bool wasActiveAdmin = existing.Activo && existing.Nivel == UserRoleLevel.Administrador;
        bool staysActiveAdmin = user.Activo && user.Nivel == UserRoleLevel.Administrador;
        if (wasActiveAdmin && !staysActiveAdmin && users.CountActiveAdmins() <= 1)
        {
            throw new InvalidOperationException("No se puede quitar privilegios ni desactivar al último Administrador activo del sistema.");
        }

        user.CodigoId = existing.CodigoId;
        user.AliasUsuario = trimmedAlias;
        user.NombreUsuario = user.NombreUsuario.Trim();
        user.ApePatUsuario = user.ApePatUsuario.Trim();
        user.ApeMatUsuario = user.ApeMatUsuario.Trim();
        user.MailUsuario = user.MailUsuario.Trim();
        user.FonoUsuario = user.FonoUsuario.Trim();
        user.NombreGrupo = targetGroup?.Nombre ?? string.Empty;
        user.Permisos.IdUsuario = user.IdUsuario;
        EnforceRolePermissionBoundary(user);

        users.Update(user, updatePwd, expectedRowVersion);
        if (user.EsSolicitante)
        {
            requesters.EnsureFromUser(user);
        }

        if (groupChanged && groups is not null)
        {
            string origenName = string.IsNullOrWhiteSpace(existing.NombreGrupo) ? "Sin grupo" : existing.NombreGrupo;
            string destinoName = targetGroup?.Nombre ?? "Sin grupo";
            groups.RecordMovement(new GroupMovementEntry
            {
                IdUsuario = user.IdUsuario,
                CodigoUsuario = existing.CodigoId,
                AliasUsuario = user.AliasUsuario,
                IdGrupoOrigen = existing.IdGrupo,
                NombreGrupoOrigen = origenName,
                IdGrupoDestino = user.IdGrupo,
                NombreGrupoDestino = destinoName,
                EjecutadoPor = session.Usuario.AliasUsuario,
                Motivo = "Cambio desde edición de usuario"
            });

            audit.Log(
                session.Usuario.AliasUsuario,
                "Grupos",
                "MOVER_USUARIO_GRUPO",
                $"Usuario '{user.AliasUsuario}' (ID {existing.CodigoId}) movido de '{origenName}' a '{destinoName}' (Resultado: OK).");
        }

        List<string> changedFields = CollectChangedFields(existing, user, updatePwd);
        string summary = changedFields.Count > 0 ? string.Join(", ", changedFields) : "Sin cambios de datos";
        audit.Log(
            session.Usuario.AliasUsuario,
            "Usuarios",
            "MODIFICAR_USUARIO",
            $"Usuario '{user.AliasUsuario}' (ID {existing.CodigoId}) actualizado [{summary}].");

        if (existing.Nivel != user.Nivel)
        {
            audit.Log(
                session.Usuario.AliasUsuario,
                "Usuarios",
                "CAMBIO_PERFIL",
                $"Perfil de '{user.AliasUsuario}' (ID {existing.CodigoId}) modificado de {existing.Nivel} a {user.Nivel}.");
        }

        if (existing.Activo != user.Activo)
        {
            audit.Log(
                session.Usuario.AliasUsuario,
                "Usuarios",
                user.Activo ? "ACTIVAR_USUARIO" : "DESACTIVAR_USUARIO",
                $"Estado de '{user.AliasUsuario}' (ID {existing.CodigoId}) cambiado a {(user.Activo ? "Activo" : "Inactivo")}.");
        }

        if (updatePwd)
        {
            audit.Log(
                session.Usuario.AliasUsuario,
                "Seguridad",
                "CAMBIO_CLAVE_ADMIN",
                $"Contraseña de '{user.AliasUsuario}' (ID {existing.CodigoId}) actualizada por administrador.");
        }

        syncHub?.Publish(DataChangeKind.Users | DataChangeKind.Groups | DataChangeKind.Audit, "MODIFICAR_USUARIO");
    }

    public void ChangeUserPassword(
        UserSession session,
        string idUsuario,
        string newPassword,
        string confirmPassword,
        bool requireChangeOnNextLogin = false)
    {
        EnsureAdministrator(session, "Cambiar contraseña de usuario");

        User existing = users.GetById(idUsuario)
            ?? throw new InvalidOperationException("El usuario seleccionado no existe.");

        string? pwdError = DomainValidator.ValidatePasswordPolicy(newPassword, confirmPassword);
        if (pwdError is not null)
        {
            throw new ArgumentException(pwdError);
        }

        string newHash = PasswordSecurity.HashPassword(newPassword);
        users.UpdatePasswordHash(existing.IdUsuario, newHash, requireChangeOnNextLogin);

        audit.Log(
            session.Usuario.AliasUsuario,
            "Seguridad",
            "CAMBIO_CLAVE_ADMIN",
            $"Contraseña del usuario '{existing.AliasUsuario}' (ID {existing.CodigoId}) restablecida por administrador (ForzarCambioPrimerAcceso={requireChangeOnNextLogin}).");

        syncHub?.Publish(DataChangeKind.Users | DataChangeKind.Audit, "CAMBIO_CLAVE_ADMIN");
    }

    /// <summary>
    /// Eliminación lógica (desactivación) de usuario. Preserva la integridad de registros históricos.
    /// </summary>
    public void DeleteUser(UserSession session, string idUsuario)
    {
        EnsureAdministrator(session, "Eliminar (desactivar) usuario");

        if (string.Equals(session.Usuario.IdUsuario, idUsuario, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("No puede eliminar ni desactivar su propia cuenta mientras mantiene la sesión activa.");
        }

        User existing = users.GetById(idUsuario)
            ?? throw new InvalidOperationException("El usuario seleccionado no existe.");

        if (!existing.Activo)
        {
            throw new InvalidOperationException($"El usuario '{existing.AliasUsuario}' ya se encuentra inactivo.");
        }

        if (existing.Nivel == UserRoleLevel.Administrador && users.CountActiveAdmins() <= 1)
        {
            throw new InvalidOperationException("No se puede eliminar ni desactivar al último Administrador activo del sistema.");
        }

        users.SetActive(existing.IdUsuario, active: false);

        audit.Log(
            session.Usuario.AliasUsuario,
            "Usuarios",
            "DESACTIVAR_USUARIO",
            $"Eliminación lógica del usuario '{existing.AliasUsuario}' (ID {existing.CodigoId}, Perfil {existing.Nivel}).");

        syncHub?.Publish(DataChangeKind.Users | DataChangeKind.Audit, "DESACTIVAR_USUARIO");
    }

    public void SetUserActive(UserSession session, string idUsuario, bool active)
    {
        if (!active)
        {
            DeleteUser(session, idUsuario);
            return;
        }

        EnsureAdministrator(session, "Activar usuario");

        User existing = users.GetById(idUsuario)
            ?? throw new InvalidOperationException("El usuario seleccionado no existe.");

        if (existing.Activo)
        {
            return;
        }

        users.SetActive(existing.IdUsuario, active: true);

        audit.Log(
            session.Usuario.AliasUsuario,
            "Usuarios",
            "ACTIVAR_USUARIO",
            $"Reactivación del usuario '{existing.AliasUsuario}' (ID {existing.CodigoId}, Perfil {existing.Nivel}).");

        syncHub?.Publish(DataChangeKind.Users | DataChangeKind.Audit, "ACTIVAR_USUARIO");
    }

    /// <summary>
    /// Movimiento individual o masivo de usuarios hacia un grupo de destino (o 'Sin grupo' cuando targetGroupId es null).
    /// Preserva la autoría histórica de órdenes de trabajo y registros previos.
    /// </summary>
    public BatchOperationResult MoveUsersToGroup(
        UserSession session,
        IReadOnlyList<string> userIds,
        int? targetGroupId,
        string motivo = "")
    {
        EnsureAdministrator(session, "Mover usuarios de grupo");
        List<string> distinctIds = NormalizeIds(userIds);
        if (distinctIds.Count == 0)
        {
            throw new ArgumentException("Debe seleccionar al menos un usuario para mover.");
        }

        UserGroup? targetGroup = ResolveAndValidateGroup(targetGroupId, requireActive: true);
        string destinoNombre = targetGroup?.Nombre ?? "Sin grupo";
        string cleanMotivo = string.IsNullOrWhiteSpace(motivo) ? "Reasignación administrativa de grupo" : motivo.Trim();

        BatchOperationResult result = new()
        {
            OperationName = $"Mover usuarios a '{destinoNombre}'",
            RequestedCount = distinctIds.Count
        };

        foreach (string id in distinctIds)
        {
            User? u = users.GetById(id);
            if (u is null)
            {
                result.Items.Add(new BatchItemOutcome
                {
                    TargetId = id,
                    TargetLabel = id,
                    Succeeded = false,
                    Message = "Usuario no encontrado."
                });
                continue;
            }

            if (u.IdGrupo == targetGroupId)
            {
                result.Items.Add(new BatchItemOutcome
                {
                    TargetId = u.IdUsuario,
                    TargetLabel = $"{u.AliasUsuario} ({u.NombreCompleto})",
                    Succeeded = true,
                    Message = $"Ya pertenecía a '{destinoNombre}'."
                });
                continue;
            }

            string origenNombre = string.IsNullOrWhiteSpace(u.NombreGrupo) ? "Sin grupo" : u.NombreGrupo;
            users.SetGroup(u.IdUsuario, targetGroupId);

            groups?.RecordMovement(new GroupMovementEntry
            {
                IdUsuario = u.IdUsuario,
                CodigoUsuario = u.CodigoId,
                AliasUsuario = u.AliasUsuario,
                IdGrupoOrigen = u.IdGrupo,
                NombreGrupoOrigen = origenNombre,
                IdGrupoDestino = targetGroupId,
                NombreGrupoDestino = destinoNombre,
                EjecutadoPor = session.Usuario.AliasUsuario,
                Motivo = cleanMotivo
            });

            audit.Log(
                session.Usuario.AliasUsuario,
                "Grupos",
                "MOVER_USUARIO_GRUPO",
                $"Usuario '{u.AliasUsuario}' (ID {u.CodigoId}) movido de '{origenNombre}' -> '{destinoNombre}' | Motivo: {cleanMotivo} | Resultado: OK.");

            result.Items.Add(new BatchItemOutcome
            {
                TargetId = u.IdUsuario,
                TargetLabel = $"{u.AliasUsuario} ({u.NombreCompleto})",
                Succeeded = true,
                Message = $"{origenNombre} → {destinoNombre}"
            });
        }

        if (result.SucceededCount > 0)
        {
            syncHub?.Publish(DataChangeKind.Users | DataChangeKind.Groups | DataChangeKind.Audit, "MOVER_USUARIO_GRUPO");
        }

        return result;
    }

    /// <summary>
    /// Activación o desactivación/eliminación lógica por lotes. Protege la sesión actual y al último Administrador activo.
    /// </summary>
    public BatchOperationResult BatchSetActive(
        UserSession session,
        IReadOnlyList<string> userIds,
        bool active,
        string motivo = "")
    {
        string opLabel = active ? "Activar usuarios por lotes" : "Desactivar / Eliminar lógicamente usuarios por lotes";
        EnsureAdministrator(session, opLabel);

        List<string> distinctIds = NormalizeIds(userIds);
        if (distinctIds.Count == 0)
        {
            throw new ArgumentException("Debe seleccionar al menos un usuario.");
        }

        string cleanMotivo = string.IsNullOrWhiteSpace(motivo) ? "Operación masiva administrativa" : motivo.Trim();
        int remainingActiveAdmins = users.CountActiveAdmins();

        BatchOperationResult result = new()
        {
            OperationName = opLabel,
            RequestedCount = distinctIds.Count
        };

        foreach (string id in distinctIds)
        {
            User? u = users.GetById(id);
            if (u is null)
            {
                result.Items.Add(new BatchItemOutcome
                {
                    TargetId = id,
                    TargetLabel = id,
                    Succeeded = false,
                    Message = "Usuario no encontrado."
                });
                continue;
            }

            string label = $"{u.AliasUsuario} ({u.NombreCompleto})";

            if (!active)
            {
                if (string.Equals(session.Usuario.IdUsuario, u.IdUsuario, StringComparison.OrdinalIgnoreCase))
                {
                    audit.Log(
                        session.Usuario.AliasUsuario,
                        "Seguridad",
                        "PROTECCION_LOTE",
                        $"Cuenta propia '{u.AliasUsuario}' omitida en desactivación masiva (Motivo: sesión activa).");
                    result.Items.Add(new BatchItemOutcome
                    {
                        TargetId = u.IdUsuario,
                        TargetLabel = label,
                        Succeeded = false,
                        ProtectedSkipped = true,
                        Message = "Cuenta de la sesión activa (protegida)."
                    });
                    continue;
                }

                if (u.Activo && u.Nivel == UserRoleLevel.Administrador && remainingActiveAdmins <= 1)
                {
                    audit.Log(
                        session.Usuario.AliasUsuario,
                        "Seguridad",
                        "PROTECCION_ULTIMO_ADMIN",
                        $"Último Administrador activo '{u.AliasUsuario}' protegido contra desactivación masiva.");
                    result.Items.Add(new BatchItemOutcome
                    {
                        TargetId = u.IdUsuario,
                        TargetLabel = label,
                        Succeeded = false,
                        ProtectedSkipped = true,
                        Message = "Último Administrador activo del sistema (protegido)."
                    });
                    continue;
                }

                if (!u.Activo)
                {
                    result.Items.Add(new BatchItemOutcome
                    {
                        TargetId = u.IdUsuario,
                        TargetLabel = label,
                        Succeeded = true,
                        Message = "Ya se encontraba inactivo."
                    });
                    continue;
                }

                users.SetActive(u.IdUsuario, active: false);
                if (u.Nivel == UserRoleLevel.Administrador)
                {
                    remainingActiveAdmins--;
                }

                audit.Log(
                    session.Usuario.AliasUsuario,
                    "Usuarios",
                    "DESACTIVAR_USUARIO_LOTE",
                    $"Eliminación lógica masiva de '{u.AliasUsuario}' (ID {u.CodigoId}, Grupo '{(string.IsNullOrWhiteSpace(u.NombreGrupo) ? "Sin grupo" : u.NombreGrupo)}') | Motivo: {cleanMotivo} | Resultado: OK.");

                result.Items.Add(new BatchItemOutcome
                {
                    TargetId = u.IdUsuario,
                    TargetLabel = label,
                    Succeeded = true,
                    Message = "Desactivado (eliminación lógica)."
                });
            }
            else
            {
                if (u.Activo)
                {
                    result.Items.Add(new BatchItemOutcome
                    {
                        TargetId = u.IdUsuario,
                        TargetLabel = label,
                        Succeeded = true,
                        Message = "Ya se encontraba activo."
                    });
                    continue;
                }

                users.SetActive(u.IdUsuario, active: true);
                if (u.Nivel == UserRoleLevel.Administrador)
                {
                    remainingActiveAdmins++;
                }

                audit.Log(
                    session.Usuario.AliasUsuario,
                    "Usuarios",
                    "ACTIVAR_USUARIO_LOTE",
                    $"Activación masiva de '{u.AliasUsuario}' (ID {u.CodigoId}) | Motivo: {cleanMotivo} | Resultado: OK.");

                result.Items.Add(new BatchItemOutcome
                {
                    TargetId = u.IdUsuario,
                    TargetLabel = label,
                    Succeeded = true,
                    Message = "Activado."
                });
            }
        }

        if (result.SucceededCount > 0)
        {
            syncHub?.Publish(DataChangeKind.Users | DataChangeKind.Audit, active ? "ACTIVAR_USUARIO_LOTE" : "DESACTIVAR_USUARIO_LOTE");
        }

        return result;
    }

    /// <summary>
    /// Cambio masivo de perfil con protección de la sesión actual y del último Administrador activo.
    /// </summary>
    public BatchOperationResult BatchChangeRole(
        UserSession session,
        IReadOnlyList<string> userIds,
        UserRoleLevel newRole,
        string motivo = "")
    {
        EnsureAdministrator(session, "Cambiar perfil por lotes");

        List<string> distinctIds = NormalizeIds(userIds);
        if (distinctIds.Count == 0)
        {
            throw new ArgumentException("Debe seleccionar al menos un usuario.");
        }

        string cleanMotivo = string.IsNullOrWhiteSpace(motivo) ? "Cambio masivo de perfil administrativo" : motivo.Trim();
        int remainingActiveAdmins = users.CountActiveAdmins();

        BatchOperationResult result = new()
        {
            OperationName = $"Cambiar perfil a '{newRole}'",
            RequestedCount = distinctIds.Count
        };

        foreach (string id in distinctIds)
        {
            User? u = users.GetById(id);
            if (u is null)
            {
                result.Items.Add(new BatchItemOutcome
                {
                    TargetId = id,
                    TargetLabel = id,
                    Succeeded = false,
                    Message = "Usuario no encontrado."
                });
                continue;
            }

            string label = $"{u.AliasUsuario} ({u.NombreCompleto})";
            bool isSelf = string.Equals(session.Usuario.IdUsuario, u.IdUsuario, StringComparison.OrdinalIgnoreCase);

            if (isSelf && newRole != UserRoleLevel.Administrador)
            {
                result.Items.Add(new BatchItemOutcome
                {
                    TargetId = u.IdUsuario,
                    TargetLabel = label,
                    Succeeded = false,
                    ProtectedSkipped = true,
                    Message = "No puede degradar el perfil de su propia sesión activa."
                });
                continue;
            }

            bool wasActiveAdmin = u.Activo && u.Nivel == UserRoleLevel.Administrador;
            bool staysActiveAdmin = u.Activo && newRole == UserRoleLevel.Administrador;
            if (wasActiveAdmin && !staysActiveAdmin && remainingActiveAdmins <= 1)
            {
                audit.Log(
                    session.Usuario.AliasUsuario,
                    "Seguridad",
                    "PROTECCION_ULTIMO_ADMIN",
                    $"Último Administrador activo '{u.AliasUsuario}' protegido contra degradación masiva de perfil.");
                result.Items.Add(new BatchItemOutcome
                {
                    TargetId = u.IdUsuario,
                    TargetLabel = label,
                    Succeeded = false,
                    ProtectedSkipped = true,
                    Message = "Último Administrador activo del sistema (protegido)."
                });
                continue;
            }

            if (u.Nivel == newRole)
            {
                result.Items.Add(new BatchItemOutcome
                {
                    TargetId = u.IdUsuario,
                    TargetLabel = label,
                    Succeeded = true,
                    Message = $"Ya tenía perfil {newRole}."
                });
                continue;
            }

            UserRoleLevel previousRole = u.Nivel;
            UserPermissions perms = PermissionPresets.CreateForRole(newRole, u.IdUsuario);
            u.Nivel = newRole;
            u.Permisos = perms;
            EnforceRolePermissionBoundary(u);

            users.SetRole(u.IdUsuario, newRole, u.Permisos);

            if (wasActiveAdmin && !staysActiveAdmin)
            {
                remainingActiveAdmins--;
            }
            else if (!wasActiveAdmin && staysActiveAdmin)
            {
                remainingActiveAdmins++;
            }

            audit.Log(
                session.Usuario.AliasUsuario,
                "Usuarios",
                "CAMBIO_PERFIL_LOTE",
                $"Perfil de '{u.AliasUsuario}' (ID {u.CodigoId}) modificado de {previousRole} -> {newRole} | Motivo: {cleanMotivo} | Resultado: OK.");

            result.Items.Add(new BatchItemOutcome
            {
                TargetId = u.IdUsuario,
                TargetLabel = label,
                Succeeded = true,
                Message = $"{previousRole} → {newRole}"
            });
        }

        if (result.SucceededCount > 0)
        {
            syncHub?.Publish(DataChangeKind.Users | DataChangeKind.Audit, "CAMBIO_PERFIL_LOTE");
        }

        return result;
    }

    /// <summary>
    /// Analiza previamente un lote de usuarios para mostrar en el diálogo de confirmación
    /// cuántos serán afectados y qué cuentas están protegidas (sesión activa / último Administrador).
    /// </summary>
    public (IReadOnlyList<User> Affected, IReadOnlyList<(User User, string Reason)> Protected) PreviewBatchImpact(
        UserSession session,
        IReadOnlyList<string> userIds,
        bool checkAdminDeactivationOrDemotion)
    {
        EnsureAdministrator(session, "Previsualizar operación masiva");
        List<string> distinctIds = NormalizeIds(userIds);
        List<User> affected = [];
        List<(User User, string Reason)> protectedList = [];
        int remainingActiveAdmins = users.CountActiveAdmins();

        foreach (string id in distinctIds)
        {
            User? u = users.GetById(id);
            if (u is null)
            {
                continue;
            }

            if (checkAdminDeactivationOrDemotion)
            {
                if (string.Equals(session.Usuario.IdUsuario, u.IdUsuario, StringComparison.OrdinalIgnoreCase))
                {
                    protectedList.Add((u, "Sesión activa actual"));
                    continue;
                }

                if (u.Activo && u.Nivel == UserRoleLevel.Administrador)
                {
                    if (remainingActiveAdmins <= 1)
                    {
                        protectedList.Add((u, "Último Administrador activo"));
                        continue;
                    }

                    remainingActiveAdmins--;
                }
            }

            affected.Add(u);
        }

        return (affected, protectedList);
    }

    private UserGroup? ResolveAndValidateGroup(int? idGrupo, bool requireActive)
    {
        if (!idGrupo.HasValue)
        {
            return null;
        }

        if (groups is null)
        {
            return null;
        }

        UserGroup group = groups.GetById(idGrupo.Value)
            ?? throw new ArgumentException("El grupo seleccionado no existe.");

        if (requireActive && !group.Activo)
        {
            throw new InvalidOperationException($"El grupo '{group.Nombre}' está inactivo y no admite nuevas asignaciones.");
        }

        return group;
    }

    private static List<string> NormalizeIds(IReadOnlyList<string> userIds) =>
        userIds.Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

    private void EnsureAdministrator(UserSession? session, string actionDescription)
    {
        if (session is null || !session.Usuario.Activo)
        {
            audit.Log(
                session?.Usuario.AliasUsuario ?? "ANONIMO",
                "Seguridad",
                "ACCESO_NO_AUTORIZADO",
                $"Intento sin sesión activa en Gestión de Usuarios: {actionDescription}.");
            throw new UnauthorizedAccessException($"Acceso denegado: no existe una sesión activa autorizada para {actionDescription}.");
        }

        User? currentDbUser = users.GetById(session.Usuario.IdUsuario);
        bool isActiveAdmin = currentDbUser is not null
                             && currentDbUser.Activo
                             && currentDbUser.Nivel == UserRoleLevel.Administrador
                             && session.Usuario.Nivel == UserRoleLevel.Administrador;

        if (!isActiveAdmin)
        {
            audit.Log(
                session.Usuario.AliasUsuario,
                "Seguridad",
                "ACCESO_NO_AUTORIZADO",
                $"Usuario '{session.Usuario.AliasUsuario}' (Perfil {session.Usuario.Nivel}) intentó sin autorización: {actionDescription}.");
            throw new UnauthorizedAccessException($"Acceso denegado: el módulo Gestión de Usuarios es exclusivo del perfil Administrador ({actionDescription}).");
        }
    }

    private static void EnforceRolePermissionBoundary(User user)
    {
        if (user.Nivel == UserRoleLevel.Administrador)
        {
            UserPermissions full = PermissionPresets.CreateForRole(UserRoleLevel.Administrador, user.IdUsuario);
            user.Permisos = full;
        }
        else
        {
            user.Permisos.MenuUsuarios = false;
            user.Permisos.IngresoUsuarios = false;
            user.Permisos.ListadoUsuarios = false;
            user.Permisos.NormalizeHierarchy();
        }
    }

    private static void ValidateCommonUserFields(User user)
    {
        if (string.IsNullOrWhiteSpace(user.AliasUsuario) || user.AliasUsuario.Trim().Length < 3)
        {
            throw new ArgumentException("El nombre de usuario debe tener al menos 3 caracteres.");
        }

        if (string.IsNullOrWhiteSpace(user.NombreUsuario) || string.IsNullOrWhiteSpace(user.ApePatUsuario))
        {
            throw new ArgumentException("Debe ingresar el nombre y apellido del usuario.");
        }

        if (!DomainValidator.IsValidEmailOrEmpty(user.MailUsuario))
        {
            throw new ArgumentException("El correo electrónico ingresado no tiene un formato válido.");
        }
    }

    private static List<string> CollectChangedFields(User before, User after, bool passwordChanged)
    {
        List<string> changes = [];
        if (!string.Equals(before.AliasUsuario, after.AliasUsuario, StringComparison.Ordinal)) changes.Add("Usuario");
        if (!string.Equals(before.NombreUsuario, after.NombreUsuario, StringComparison.Ordinal)) changes.Add("Nombre");
        if (!string.Equals(before.Apellidos, after.Apellidos, StringComparison.Ordinal)) changes.Add("Apellido");
        if (!string.Equals(before.MailUsuario, after.MailUsuario, StringComparison.Ordinal)) changes.Add("Correo");
        if (!string.Equals(before.FonoUsuario, after.FonoUsuario, StringComparison.Ordinal)) changes.Add("Teléfono");
        if (before.Nivel != after.Nivel) changes.Add($"Perfil({before.Nivel}->{after.Nivel})");
        if (before.IdGrupo != after.IdGrupo)
        {
            string g1 = string.IsNullOrWhiteSpace(before.NombreGrupo) ? "Sin grupo" : before.NombreGrupo;
            string g2 = string.IsNullOrWhiteSpace(after.NombreGrupo) ? "Sin grupo" : after.NombreGrupo;
            changes.Add($"Grupo({g1}->{g2})");
        }
        if (before.Activo != after.Activo) changes.Add($"Estado({(after.Activo ? "Activo" : "Inactivo")})");
        if (before.EsSolicitante != after.EsSolicitante) changes.Add("Solicitante");
        if (passwordChanged) changes.Add("Contraseña");
        return changes;
    }
}
