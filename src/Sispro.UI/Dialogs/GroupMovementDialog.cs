using System.Drawing;
using Sispro.Core.Entities;
using Sispro.UI.Theme;

namespace Sispro.UI.Dialogs;

/// <summary>
/// Diálogo de confirmación para mover uno o múltiples usuarios hacia un grupo de destino.
/// </summary>
public sealed class GroupMovementDialog : Form
{
    private readonly ComboBox _cmbDestino;
    private readonly TextBox _txtMotivo;

    public int? SelectedTargetGroupId => (_cmbDestino.SelectedItem as TargetGroupItem)?.IdGrupo;
    public string SelectedTargetGroupName => (_cmbDestino.SelectedItem as TargetGroupItem)?.Label ?? "Sin grupo";
    public string Motivo => _txtMotivo.Text.Trim();

    public GroupMovementDialog(IReadOnlyList<User> usersToMove, IReadOnlyList<UserGroup> activeGroups, int? preselectedGroupId = null)
    {
        Text = "Mover Usuarios de Grupo — SISPRO";
        Icon = IconCatalog.GetAppIcon();
        Size = new Size(520, 420);
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
            BackColor = CambiasoTheme.PrimaryDark,
            Padding = new Padding(16, 10, 16, 10)
        };
        header.Controls.Add(new Label
        {
            Text = $"MOVER {usersToMove.Count} USUARIO(S) DE GRUPO",
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
        body.RowStyles.Add(new RowStyle(SizeType.Absolute, 22));
        body.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
        body.RowStyles.Add(new RowStyle(SizeType.Absolute, 22));
        body.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
        body.RowStyles.Add(new RowStyle(SizeType.Absolute, 22));
        body.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        List<TargetGroupItem> items = [new TargetGroupItem(null, "(Sin grupo asignado)")];
        foreach (UserGroup g in activeGroups.Where(g => g.Activo))
        {
            items.Add(new TargetGroupItem(g.IdGrupo, $"{g.Nombre} ({g.CantidadIntegrantes} integrantes)"));
        }

        _cmbDestino = new ComboBox
        {
            Name = "cmbGrupoDestino",
            DropDownStyle = ComboBoxStyle.DropDownList,
            Dock = DockStyle.Fill
        };
        foreach (TargetGroupItem item in items)
        {
            _cmbDestino.Items.Add(item);
        }

        if (preselectedGroupId.HasValue)
        {
            int idx = items.FindIndex(i => i.IdGrupo == preselectedGroupId.Value);
            _cmbDestino.SelectedIndex = idx >= 0 ? idx : 0;
        }
        else
        {
            _cmbDestino.SelectedIndex = _cmbDestino.Items.Count > 1 ? 1 : 0;
        }

        _txtMotivo = new TextBox
        {
            Name = "txtMotivoMovimiento",
            MaxLength = 200,
            PlaceholderText = "Ej. Cambio de turno / reorganización de taller...",
            Dock = DockStyle.Fill
        };

        ListBox lstAffected = new()
        {
            Dock = DockStyle.Fill,
            BorderStyle = BorderStyle.FixedSingle,
            Font = CambiasoTheme.SmallFont
        };
        foreach (User u in usersToMove)
        {
            string grp = string.IsNullOrWhiteSpace(u.NombreGrupo) ? "Sin grupo" : u.NombreGrupo;
            lstAffected.Items.Add($"#{u.CodigoId}  {u.AliasUsuario} — {u.NombreCompleto}  [Actual: {grp}]");
        }

        body.Controls.Add(CambiasoTheme.CreateFieldLabel("Grupo de destino *:"), 0, 0);
        body.Controls.Add(_cmbDestino, 0, 1);
        body.Controls.Add(CambiasoTheme.CreateFieldLabel("Motivo del movimiento (opcional, queda auditado):"), 0, 2);
        body.Controls.Add(_txtMotivo, 0, 3);
        body.Controls.Add(CambiasoTheme.CreateFieldLabel($"Usuarios afectados ({usersToMove.Count}) — se preservará su historial de OTs y operaciones:"), 0, 4);
        body.Controls.Add(lstAffected, 0, 5);

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

        Button btnConfirm = CambiasoTheme.CreateButton("Confirmar Movimiento", AppIcon.MoveRight, ButtonTone.Primary, width: 178, height: 32);
        btnConfirm.Click += (_, _) => DialogResult = DialogResult.OK;

        footer.Controls.AddRange([btnCancel, btnConfirm]);
        AcceptButton = btnConfirm;
        CancelButton = btnCancel;

        Controls.Add(body);
        Controls.Add(header);
        Controls.Add(footer);
    }

    private sealed record TargetGroupItem(int? IdGrupo, string Label)
    {
        public override string ToString() => Label;
    }
}
