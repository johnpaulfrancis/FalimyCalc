using System.Text;
using ClosedXML.Excel;
using FalimyCalc.Data.Entities;
using FalimyCalc.Data.Services;

namespace FalimyCalc.Web.Services;

public class ExportService
{
    /// <summary>
    /// Generates a standard UTF-8 CSV string with BOM for Excel compatibility.
    /// </summary>
    public byte[] ExportExpensesToCsv(IEnumerable<Expense> expenses)
    {
        var sb = new StringBuilder();
        // CSV Header
        sb.AppendLine("Date,Category,Description,Amount (INR)");

        foreach (var e in expenses)
        {
            var date = e.TransactionDate.ToString("yyyy-MM-dd");
            var category = EscapeCsv(e.Category?.Name ?? "Uncategorised");
            var desc = EscapeCsv(e.Description);
            var amount = e.Amount.ToString("0.00");

            sb.AppendLine($"{date},{category},{desc},{amount}");
        }

        // Return UTF8 with BOM so Excel correctly parses special chars/emojis
        return Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
    }

    /// <summary>
    /// Generates an Excel (.xlsx) workbook with exact 10 columns matching the standard import/export format:
    /// ID | Amount | Date | Title | Note | Account | Currency | Category | Subcategory | Tags
    /// </summary>
    public byte[] ExportExpensesToExcel(IEnumerable<Expense> expenses, string title = "Expenses Report")
    {
        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add("Expenses");

        // Headers matching the requested format:
        // ID | Amount | Date | Title | Note | Account | Currency | Category | Subcategory | Tags
        string[] headers = ["ID", "Amount", "Date", "Title", "Note", "Account", "Currency", "Category", "Subcategory", "Tags"];

        for (int c = 0; c < headers.Length; c++)
        {
            var cell = ws.Cell(1, c + 1);
            cell.Value = headers[c];
            cell.Style.Font.Bold = true;
            cell.Style.Font.FontColor = XLColor.White;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#1f2937"); // Clean dark header
            cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        }
        ws.Row(1).Height = 22;

        int row = 2;
        foreach (var e in expenses)
        {
            // ID (GlobalId)
            ws.Cell(row, 1).Value = e.GlobalId.ToString();
            ws.Cell(row, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;

            // Amount: exported as negative for expenses (e.g. -409) matching user's sheet convention
            var amountCell = ws.Cell(row, 2);
            amountCell.Value = -(double)e.Amount;
            amountCell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;

            // Date: formatted as dd-MM-yyyy HH:mm
            ws.Cell(row, 3).Value = e.TransactionDate.ToString("dd-MM-yyyy HH:mm");
            ws.Cell(row, 3).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            // Title
            ws.Cell(row, 4).Value = e.Description;

            // Note
            ws.Cell(row, 5).Value = e.Note ?? "";

            // Account
            ws.Cell(row, 6).Value = e.BankAccount?.AccountName ?? e.Account ?? "Transaction Bank";

            // Currency
            ws.Cell(row, 7).Value = string.IsNullOrWhiteSpace(e.Currency) ? "INR" : e.Currency;
            ws.Cell(row, 7).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            // Category
            ws.Cell(row, 8).Value = e.Category?.Name ?? "Uncategorised";

            // Subcategory
            ws.Cell(row, 9).Value = e.Subcategory ?? "";

            // Tags
            ws.Cell(row, 10).Value = e.Tags ?? "";

            row++;
        }

        ws.Columns(1, 10).AdjustToContents();

        using var ms = new MemoryStream();
        workbook.SaveAs(ms);
        return ms.ToArray();
    }

    /// <summary>
    /// Parses an uploaded Excel (.xlsx) stream into Expense items according to the 10-column header schema.
    /// Handles flexible column placement by inspecting header row names.
    /// </summary>
    public List<ExpenseService.ImportExpenseItem> ParseExpensesFromExcel(Stream stream)
    {
        using var workbook = new XLWorkbook(stream);
        var ws = workbook.Worksheets.FirstOrDefault();
        if (ws is null)
            throw new InvalidOperationException("The uploaded Excel workbook contains no worksheets.");

        var items = new List<ExpenseService.ImportExpenseItem>();

        // Find header row (usually row 1)
        int headerRow = 1;
        var colMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        for (int col = 1; col <= ws.LastColumnUsed()?.ColumnNumber(); col++)
        {
            var headerVal = ws.Cell(headerRow, col).GetString().Trim();
            if (!string.IsNullOrEmpty(headerVal) && !colMap.ContainsKey(headerVal))
            {
                colMap[headerVal] = col;
            }
        }

        // Helper to get column index with fallbacks
        int GetCol(params string[] aliases)
        {
            foreach (var alias in aliases)
            {
                if (colMap.TryGetValue(alias, out var colIndex))
                    return colIndex;
            }
            return -1;
        }

        int idCol = GetCol("ID", "Id", "Guid", "GlobalId", "TransactionId", "Transaction Id", "TxId", "TxnId", "Txn Id", "Reference", "ReferenceId", "RefId", "Ref");
        int amountCol = GetCol("Amount", "Amt", "Cost");
        int dateCol = GetCol("Date", "Transaction Date", "TransactionDate", "Time");
        int titleCol = GetCol("Title", "Description", "Desc", "Merchant");
        int noteCol = GetCol("Note", "Notes", "Comment", "Remarks");
        int accountCol = GetCol("Account", "Bank", "Account Name", "Payment Mode");
        int currencyCol = GetCol("Currency", "Curr");
        int categoryCol = GetCol("Category", "Cat");
        int subcategoryCol = GetCol("Subcategory", "Sub Category", "Sub-category");
        int tagsCol = GetCol("Tags", "Tag", "Label");

        if (amountCol == -1 && titleCol == -1)
        {
            throw new InvalidOperationException("Could not find required 'Amount' or 'Title' columns in the Excel file.");
        }

        int lastRow = ws.LastRowUsed()?.RowNumber() ?? 0;

        for (int r = headerRow + 1; r <= lastRow; r++)
        {
            // Skip empty rows
            if (ws.Row(r).IsEmpty()) continue;

            // ID / External Identifier
            Guid? globalId = null;
            string? externalId = null;
            if (idCol != -1)
            {
                var idStr = ws.Cell(r, idCol).GetString().Trim();
                if (!string.IsNullOrEmpty(idStr))
                {
                    externalId = idStr;
                    if (Guid.TryParse(idStr, out var parsedGuid))
                    {
                        globalId = parsedGuid;
                    }
                }
            }

            // Amount
            decimal amount = 0;
            if (amountCol != -1)
            {
                var amountCell = ws.Cell(r, amountCol);
                if (amountCell.TryGetValue(out double dVal))
                {
                    amount = (decimal)dVal;
                }
                else
                {
                    var amtStr = amountCell.GetString().Replace("₹", "").Replace(",", "").Trim();
                    decimal.TryParse(amtStr, out amount);
                }
            }

            // If amount is 0 and title is blank, skip
            var title = titleCol != -1 ? ws.Cell(r, titleCol).GetString().Trim() : "";
            if (amount == 0 && string.IsNullOrWhiteSpace(title))
                continue;

            // Date
            DateTime date = DateTime.Today;
            if (dateCol != -1)
            {
                var dateCell = ws.Cell(r, dateCol);
                if (dateCell.TryGetValue(out DateTime dtVal))
                {
                    date = dtVal;
                }
                else
                {
                    var dateStr = dateCell.GetString().Trim();
                    string[] formats = ["dd-MM-yyyy HH:mm", "dd-MM-yyyy", "yyyy-MM-dd HH:mm", "yyyy-MM-dd", "dd/MM/yyyy HH:mm", "dd/MM/yyyy", "M/d/yyyy h:mm tt", "M/d/yyyy"];
                    if (DateTime.TryParseExact(dateStr, formats, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var parsedDt))
                    {
                        date = parsedDt;
                    }
                    else if (DateTime.TryParse(dateStr, out var fallbackDt))
                    {
                        date = fallbackDt;
                    }
                }
            }

            // Note
            string? note = noteCol != -1 ? ws.Cell(r, noteCol).GetString().Trim() : null;
            if (string.IsNullOrEmpty(note)) note = null;

            // Account
            string? account = accountCol != -1 ? ws.Cell(r, accountCol).GetString().Trim() : null;
            if (string.IsNullOrEmpty(account)) account = "Transaction Bank";

            // Currency
            string? currency = currencyCol != -1 ? ws.Cell(r, currencyCol).GetString().Trim() : null;
            if (string.IsNullOrEmpty(currency)) currency = "INR";

            // Category
            string? category = categoryCol != -1 ? ws.Cell(r, categoryCol).GetString().Trim() : null;
            if (string.IsNullOrEmpty(category)) category = "Uncategorised";

            // Subcategory
            string? subcategory = subcategoryCol != -1 ? ws.Cell(r, subcategoryCol).GetString().Trim() : null;
            if (string.IsNullOrEmpty(subcategory)) subcategory = null;

            // Tags
            string? tags = tagsCol != -1 ? ws.Cell(r, tagsCol).GetString().Trim() : null;
            if (string.IsNullOrEmpty(tags)) tags = null;

            items.Add(new ExpenseService.ImportExpenseItem(
                globalId,
                amount,
                date,
                string.IsNullOrWhiteSpace(title) ? "Expense" : title,
                note,
                account,
                currency,
                category,
                subcategory,
                tags,
                externalId
            ));
        }

        return items;
    }

    /// <summary>
    /// Generates an Excel (.xlsx) file containing Category Breakdown and Monthly Trends summary.
    /// </summary>
    public byte[] ExportReportToExcel(
        string periodLabel,
        decimal monthTotal,
        int totalTransactions,
        List<ExpenseService.CategorySummaryItem> categorySummaries,
        List<ExpenseService.MonthlyTrendItem> trends)
    {
        using var workbook = new XLWorkbook();

        // Sheet 1: Category Breakdown
        var wsCat = workbook.Worksheets.Add("Category Summary");

        wsCat.Cell("A1").Value = $"Spending Report — {periodLabel}";
        wsCat.Range("A1:D1").Merge();
        wsCat.Cell("A1").Style.Font.Bold = true;
        wsCat.Cell("A1").Style.Font.FontSize = 16;
        wsCat.Cell("A1").Style.Font.FontColor = XLColor.FromHtml("#4f46e5");
        wsCat.Row(1).Height = 28;

        wsCat.Cell("A2").Value = $"Total Spending: ₹ {monthTotal:N2}  |  Transactions: {totalTransactions}";
        wsCat.Range("A2:D2").Merge();
        wsCat.Cell("A2").Style.Font.Italic = true;
        wsCat.Cell("A2").Style.Font.FontColor = XLColor.FromHtml("#4b5563");
        wsCat.Row(2).Height = 20;

        int catHeader = 4;
        wsCat.Cell(catHeader, 1).Value = "Category";
        wsCat.Cell(catHeader, 2).Value = "Transactions";
        wsCat.Cell(catHeader, 3).Value = "Total Amount (₹)";
        wsCat.Cell(catHeader, 4).Value = "% of Total";

        var catHRange = wsCat.Range(catHeader, 1, catHeader, 4);
        catHRange.Style.Font.Bold = true;
        catHRange.Style.Font.FontColor = XLColor.White;
        catHRange.Style.Fill.BackgroundColor = XLColor.FromHtml("#4f46e5");
        catHRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        wsCat.Row(catHeader).Height = 24;

        int r = catHeader + 1;
        foreach (var cat in categorySummaries)
        {
            wsCat.Cell(r, 1).Value = $"{cat.Icon} {cat.Name}";
            wsCat.Cell(r, 2).Value = cat.Count;
            wsCat.Cell(r, 2).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            var cellAmt = wsCat.Cell(r, 3);
            cellAmt.Value = (double)cat.Total;
            cellAmt.Style.NumberFormat.Format = "₹ #,##0.00";
            cellAmt.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;

            var cellPct = wsCat.Cell(r, 4);
            var pct = monthTotal > 0 ? (double)(cat.Total / monthTotal) : 0;
            cellPct.Value = pct;
            cellPct.Style.NumberFormat.Format = "0.0%";
            cellPct.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;

            if (r % 2 == 0)
                wsCat.Range(r, 1, r, 4).Style.Fill.BackgroundColor = XLColor.FromHtml("#f9fafb");

            r++;
        }

        // Summary row
        wsCat.Cell(r, 1).Value = "Total";
        wsCat.Cell(r, 1).Style.Font.Bold = true;
        wsCat.Cell(r, 2).Value = totalTransactions;
        wsCat.Cell(r, 2).Style.Font.Bold = true;
        wsCat.Cell(r, 2).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

        var totalSumCell = wsCat.Cell(r, 3);
        totalSumCell.Value = (double)monthTotal;
        totalSumCell.Style.NumberFormat.Format = "₹ #,##0.00";
        totalSumCell.Style.Font.Bold = true;
        totalSumCell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;

        wsCat.Cell(r, 4).Value = 1.0;
        wsCat.Cell(r, 4).Style.NumberFormat.Format = "100.0%";
        wsCat.Cell(r, 4).Style.Font.Bold = true;
        wsCat.Cell(r, 4).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;

        var catTotalRange = wsCat.Range(r, 1, r, 4);
        catTotalRange.Style.Border.TopBorder = XLBorderStyleValues.Thin;
        catTotalRange.Style.Border.BottomBorder = XLBorderStyleValues.Double;
        catTotalRange.Style.Fill.BackgroundColor = XLColor.FromHtml("#eef2ff");

        wsCat.Columns(1, 4).AdjustToContents();

        // Sheet 2: 6-Month Trends
        var wsTrend = workbook.Worksheets.Add("Spending Trends");
        wsTrend.Cell("A1").Value = "Spending Trends (Recent Months)";
        wsTrend.Range("A1:C1").Merge();
        wsTrend.Cell("A1").Style.Font.Bold = true;
        wsTrend.Cell("A1").Style.Font.FontSize = 14;
        wsTrend.Cell("A1").Style.Font.FontColor = XLColor.FromHtml("#4f46e5");

        int trHeader = 3;
        wsTrend.Cell(trHeader, 1).Value = "Month";
        wsTrend.Cell(trHeader, 2).Value = "Year";
        wsTrend.Cell(trHeader, 3).Value = "Total Spending (₹)";

        var trHRange = wsTrend.Range(trHeader, 1, trHeader, 3);
        trHRange.Style.Font.Bold = true;
        trHRange.Style.Font.FontColor = XLColor.White;
        trHRange.Style.Fill.BackgroundColor = XLColor.FromHtml("#059669");
        trHRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

        int trRow = trHeader + 1;
        foreach (var t in trends)
        {
            wsTrend.Cell(trRow, 1).Value = t.MonthName;
            wsTrend.Cell(trRow, 2).Value = t.Year;
            wsTrend.Cell(trRow, 2).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            var amt = wsTrend.Cell(trRow, 3);
            amt.Value = (double)t.Total;
            amt.Style.NumberFormat.Format = "₹ #,##0.00";
            amt.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;

            trRow++;
        }
        wsTrend.Columns(1, 3).AdjustToContents();

        using var ms = new MemoryStream();
        workbook.SaveAs(ms);
        return ms.ToArray();
    }

    private static string EscapeCsv(string? value)
    {
        if (string.IsNullOrEmpty(value)) return "";
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r'))
        {
            return $"\"{value.Replace("\"", "\"\"")}\"";
        }
        return value;
    }
}
