#nullable enable

namespace DeskPulse;

partial class LogEntryDetailsForm
{
    private System.ComponentModel.IContainer? components = null;
    private System.Windows.Forms.DataGridView detailsGrid = null!;

    protected override void Dispose(bool disposing)
    {
        if (disposing) components?.Dispose();
        base.Dispose(disposing);
    }

    private void InitializeComponent()
    {
        components = new System.ComponentModel.Container();
        var bottomPanel = new System.Windows.Forms.FlowLayoutPanel();
        var closeButton = new System.Windows.Forms.Button();
        var copyButton = new System.Windows.Forms.Button();
        detailsGrid = new System.Windows.Forms.DataGridView();
        ((System.ComponentModel.ISupportInitialize)detailsGrid).BeginInit();
        SuspendLayout();
        detailsGrid.AllowUserToAddRows = false;
        detailsGrid.AllowUserToDeleteRows = false;
        detailsGrid.AllowUserToResizeRows = true;
        detailsGrid.AutoSizeRowsMode = System.Windows.Forms.DataGridViewAutoSizeRowsMode.AllCells;
        detailsGrid.ColumnHeadersVisible = false;
        detailsGrid.Columns.Add(new System.Windows.Forms.DataGridViewTextBoxColumn { HeaderText = "Field", Width = 180, ReadOnly = true, SortMode = System.Windows.Forms.DataGridViewColumnSortMode.NotSortable });
        detailsGrid.Columns.Add(new System.Windows.Forms.DataGridViewTextBoxColumn { HeaderText = "Value", AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.Fill, ReadOnly = true, SortMode = System.Windows.Forms.DataGridViewColumnSortMode.NotSortable });
        detailsGrid.Dock = System.Windows.Forms.DockStyle.Fill;
        detailsGrid.RowHeadersVisible = false;
        detailsGrid.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.CellSelect;
        bottomPanel.Dock = System.Windows.Forms.DockStyle.Bottom;
        bottomPanel.FlowDirection = System.Windows.Forms.FlowDirection.RightToLeft;
        bottomPanel.Height = 48;
        bottomPanel.Padding = new System.Windows.Forms.Padding(8);
        closeButton.DialogResult = System.Windows.Forms.DialogResult.OK;
        closeButton.Size = new System.Drawing.Size(90, 29);
        closeButton.Text = "Close";
        copyButton.Size = new System.Drawing.Size(100, 29);
        copyButton.Text = "Copy all";
        copyButton.Click += CopyButton_Click;
        bottomPanel.Controls.Add(closeButton);
        bottomPanel.Controls.Add(copyButton);
        AcceptButton = closeButton;
        CancelButton = closeButton;
        AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
        AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
        ClientSize = new System.Drawing.Size(720, 620);
        Controls.Add(detailsGrid);
        Controls.Add(bottomPanel);
        MinimumSize = new System.Drawing.Size(560, 420);
        StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
        Text = "DeskPulse - Log Entry Details";
        ((System.ComponentModel.ISupportInitialize)detailsGrid).EndInit();
        ResumeLayout(false);
    }
}
