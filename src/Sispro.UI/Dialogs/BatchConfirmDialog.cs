using System.Drawing;
using Sispro.Core.Entities;
using Sispro.Core.Enums;
using Sispro.UI.Theme;

namespace Sispro.UI.Dialogs;

/// <summary>
/// Diálogo de confirmación detallada para operaciones masivas (Activar, Desactivar/Eliminar,
/// Cambiar Perfil), mostrando cantidad seleccionada, usuarios afectados y cuentas protegidas.
/// </summary>
public sealed class BatchConfirmDialog : Form
{
    private readonly ComboBox? _cmbTargetRole;
    private readonly TextBox _txtMotivo;

    public int SelectedProfileId => _cmbTargetRole?.SelectedItem switch
    {
        UserProfile prof => prof.IdNivel,
        UserRoleLevel lvl => (int)lvl,
        _ => (int)UserRoleLevel.Usuario
    };

    public UserRoleLevel SelectedRole =>
        Enum.IsDefined(typeof(UserRoleLevel), SelectedProfileId)
            ? (UserRoleLevel)SelectedProfileId
            : UserRoleLevel.Usuario;

    public string Motivo => _txtMotivo.Text.Trim();

    public BatchConfirmDialog(
        string operationTitle,
        string warningMessage,
        int totalSelected,
        IReadOnlyList<User> affectedUsers,
        IReadOnlyList<(User User, string Reason)> protectedUsers,
        bool requireRoleSelection = false,
        bool isDestructive = false,
        IReadOnlyList<UserProfile>? availableProfiles = null)
    {
        Text = $"Confirmar {operationTitle} — SISPRO";
        Icon = IconCatalog.GetAppIcon();
        Size = new Size(580, requireRoleSelection ? 490 : 445);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        BackColor = CambiasoTheme.Surface;
        Font = CambiasoTheme.BodyFont;

        Panel header = new()
        {
            Dock = DockStyle.Top,
            Height = 52,
            BackColor = isDestructive ? CambiasoTheme.Danger : CambiasoTheme.PrimaryDark,
            Padding = new Padding(16, 10, 16, 10)
        };
        header.Controls.Add(new Label
        {
            Text = $"{operationTitle.ToUpperInvariant()} ({totalSelected} SELECCIONADO/S)",
            Font = CambiasoTheme.SectionFont,
            ForeColor = Color.White,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft
        });

        TableLayoutPanel body = new()
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 6,
            Padding = new Padding(16, 10, 16, 8)
        };
        body.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        body.RowStyles.Add(new RowStyle(SizeType.Absolute, requireRoleSelection ? 56 : 0));
        body.RowStyles.Add(new RowStyle(SizeType.Absolute, 56));
        body.RowStyles.Add(new RowStyle(SizeType.Absolute, 22));
        body.RowStyles.Add(new RowStyle(SizeType.Percent, 62));
        body.RowStyles.Add(new RowStyle(SizeType.Percent, 38));

        Label lblSummary = new()
        {
            Text = $"{warningMessage}\nSeleccionados: {totalSelected}   |   Se aplicará a: {affectedUsers.Count}   |   Cuentas protegidas (omitidas): {protectedUsers.Count}",
            AutoSize = true,
            MaximumSize = new Size(530, 0),
            ForeColor = CambiasoTheme.TextPrimary,
            Margin = new Padding(0, 0, 0, 8)
        };

        Panel rolePanel = new() { Dock = DockStyle.Fill, Visible = requireRoleSelection };
        if (requireRoleSelection)
        {
            Label lblRole = CambiasoTheme.CreateFieldLabel("Nuevo perfil a asignar *:");
            lblRole.Location = new Point(0, 2);
            _cmbTargetRole = new ComboBox
            {
                Name = "cmbNuevoPerfilLote",
                DropDownStyle = ComboBoxStyle.DropDownList,
                Location = new Point(0, 22),
                Width = 260
            };
            if (availableProfiles is { Count: > 0 })
            {
                List<UserProfile> activeProfiles = availableProfiles.Where(p => p.Activo).ToList();
                foreach (UserProfile prof in activeProfiles)
                {
                    _cmbTargetRole.Items.Add(prof);
                }

                int defaultIdx = activeProfiles.FindIndex(p => p.IdNivel == (int)UserRoleLevel.Supervisor);
                if (defaultIdx >= 0) _cmbTargetRole.SelectedIndex = defaultIdx;
                else if (_cmbTargetRole.Items.Count > 0) _cmbTargetRole.SelectedIndex = 0;
            }
            else
            {
                foreach (UserRoleLevel lvl in Enum.GetValues<UserRoleLevel>())
                {
                    _cmbTargetRole.Items.Add(lvl);
                }
                _cmbTargetRole.SelectedItem = UserRoleLevel.Supervisor;
            }
            rolePanel.Controls.AddRange([lblRole, _cmbTargetRole]);
        }

        Panel motivoPanel = new() { Dock = DockStyle.Fill };
        Label lblMotivo = CambiasoTheme.CreateFieldLabel("Motivo (registrado en auditoría individual):");
        lblMotivo.Location = new Point(0, 2);
        _txtMotivo = new TextBox
        {
            Name = "txtMotivoLote",
            Location = new Point(0, 22),
            Width = 530,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
            MaxLength = 200,
            PlaceholderText = "Ej. Revisión periódica de cuentas activas..."
        };
        motivoPanel.Controls.AddRange([lblMotivo, _txtMotivo]);

        ListBox lstAffected = new()
        {
            Dock = DockStyle.Fill,
            BorderStyle = BorderStyle.FixedSingle,
            Font = CambiasoTheme.SmallFont
        };
        foreach (User u in affectedUsers)
        {
            string grp = string.IsNullOrWhiteSpace(u.NombreGrupo) ? "Sin grupo" : u.NombreGrupo;
            lstAffected.Items.Add($"[APLICAR] #{u.CodigoId} {u.AliasUsuario} — {u.NombreCompleto} ({u.PerfilDisplay} | {grp})");
        }
        if (affectedUsers.Count == 0)
        {
            lstAffected.Items.Add("(Ningún usuario elegible tras aplicar reglas de protección)");
        }

        GroupBox grpProtected = CambiasoTheme.CreateGroupBox($"CUENTAS PROTEGIDAS ({protectedUsers.Count}) — NO SERÁN MODIFICADAS");
        grpProtected.Dock = DockStyle.Fill;
        grpProtected.ForeColor = protectedUsers.Count > 0 ? CambiasoTheme.Warning : CambiasoTheme.TextMuted;
        ListBox lstProtected = new()
        {
            Dock = DockStyle.Fill,
            BorderStyle = BorderStyle.None,
            BackColor = protectedUsers.Count > 0 ? Color.FromArgb(255, 250, 235) : CambiasoTheme.CardBackground,
            Font = CambiasoTheme.SmallFont
        };
        foreach ((User u, string reason) in protectedUsers)
        {
            lstProtected.Items.Add($"[PROTEGIDA] {u.AliasUsuario} ({u.NombreCompleto}) — {reason}");
        }
        if (protectedUsers.Count == 0)
        {
            lstProtected.Items.Add("Sin cuentas protegidas en la selección.");
        }
        grpProtected.Controls.Add(lstProtected);

        body.Controls.Add(lblSummary, 0, 0);
        body.Controls.Add(rolePanel, 0, 1);
        body.Controls.Add(motivoPanel, 0, 2);
        body.Controls.Add(CambiasoTheme.CreateFieldLabel($"Usuarios que serán procesados ({affectedUsers.Count}):"), 0, 3);
        body.Controls.Add(lstAffected, 0, 4);
        body.Controls.Add(grpProtected, 0, 5);

        FlowLayoutPanel footer = new()
        {
            Dock = DockStyle.Bottom,
            Height = 52,
            FlowDirection = FlowDirection.RightToLeft,
            BackColor = CambiasoTheme.CardBackground,
            Padding = new Padding(12, 9, 12, 8)
        };

        Button btnCancel = CambiasoTheme.CreateButton("Cancelar", AppIcon.Cancel, ButtonTone.Secondary, width: 108, height: 32);
        btnCancel.Click += (_, _) => DialogResult = DialogResult.Cancel;

        Button btnOk = CambiasoTheme.CreateButton(
            "Confirmar y Ejecutar",
            AppIcon.Check,
            isDestructive ? ButtonTone.Danger : ButtonTone.Primary,
            width: 178,
            height: 32);
        btnOk.Enabled = affectedUsers.Count > 0;
        btnOk.Click += (_, _) => DialogResult = DialogResult.OK;

        footer.Controls.AddRange([btnCancel, btnOk]);
        AcceptButton = btnOk;
        CancelButton = btnCancel;

        Controls.Add(body);
        Controls.Add(header);
        Controls.Add(footer);
    }
}
