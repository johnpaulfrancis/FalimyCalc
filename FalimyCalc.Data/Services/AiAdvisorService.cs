using FalimyCalc.Data.Entities;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FalimyCalc.Data.Services;

public class AiAdvisorService
{
    private readonly ExpenseService _expenseService;
    private readonly HttpClient _httpClient;

    public AiAdvisorService(ExpenseService expenseService, HttpClient httpClient)
    {
        _expenseService = expenseService;
        _httpClient = httpClient;
    }

    #region Models

    public record AiAnalysisReport(
        FamilyMember? Profile,
        bool IsFamilyWide,
        string ProfileDisplayName,
        int OverallHealthScore,
        string HealthTier,
        string HealthColor,
        string HealthTagline,
        int SavingsScore,
        int RunwayScore,
        int BudgetControlScore,
        int GoalsProgressScore,
        int FixedCommitmentsScore,
        decimal TotalNetBalance,
        decimal CurrentMonthIncome,
        decimal CurrentMonthExpense,
        decimal NetCashFlow,
        double SavingsRatePct,
        decimal DailyBurnRate,
        double CashRunwayMonths,
        BudgetRuleSplit Rule50_30_20,
        List<AiInsightItem> KeyWins,
        List<AiInsightItem> Vulnerabilities,
        List<AiRecommendation> Recommendations,
        List<GoalFeasibilityAssessment> GoalAssessments,
        List<SubscriptionAuditItem> SubscriptionAudits,
        string ExecutiveSummary,
        DateTime GeneratedAt
    );

    public record BudgetRuleSplit(
        decimal NeedsAmount,
        double NeedsPct,
        decimal WantsAmount,
        double WantsPct,
        decimal SavingsAmount,
        double SavingsPct,
        string Verdict
    );

    public record AiInsightItem(
        string Icon,
        string Title,
        string Description,
        string Type // "success", "warning", "info", "danger"
    );

    public record AiRecommendation(
        string Id,
        string Category,     // "Savings", "Spending", "Goals", "Subscriptions", "Safety"
        string Priority,     // "High", "Medium", "Opportunity"
        string Icon,
        string Title,
        string Description,
        decimal? EstimatedMonthlyImpact,
        string ActionUrl,
        string ActionLabel
    );

    public record GoalFeasibilityAssessment(
        Guid GoalGlobalId,
        string GoalName,
        string Icon,
        string Colour,
        decimal TargetAmount,
        decimal CurrentAmount,
        double ProgressPct,
        DateTime? TargetDate,
        decimal MonthlyVelocity,
        decimal RequiredMonthlyToTarget,
        string FeasibilityStatus, // "Completed", "On Track", "Needs Acceleration", "At Risk"
        string Advice
    );

    public record SubscriptionAuditItem(
        string Name,
        string Frequency,
        decimal Amount,
        decimal MonthlyCost,
        double IncomeRatioPct,
        string Status,
        string Assessment
    );

    public record AiAdviceReply(
        string AnswerText,
        List<string> SuggestedFollowUps,
        bool IsAiModelGenerated
    );

    #endregion

    #region Analysis Generation

    public async Task<AiAnalysisReport> GenerateReportAsync(Guid? memberGlobalId, string? geminiApiKey = null)
    {
        var now = DateTime.Now;

        // 1. Resolve Profile
        FamilyMember? profile = null;
        bool isFamilyWide = !memberGlobalId.HasValue;
        string profileName = "All Family";

        if (memberGlobalId.HasValue)
        {
            var members = await _expenseService.GetFamilyMembersAsync();
            profile = members.FirstOrDefault(m => m.GlobalId == memberGlobalId.Value);
            profileName = profile?.Name ?? "Member";
        }

        // 2. Query Underlying Financial Data
        var kpis = await _expenseService.GetFinancialKpiSummaryAsync(memberGlobalId, 30);
        var budgetStatuses = await _expenseService.GetCategoryBudgetStatusesAsync(now.Year, now.Month, memberGlobalId);
        var categoryTotals = await _expenseService.GetMonthlyTotalsByCategoryAsync(now.Year, now.Month, memberGlobalId);
        var goalsSummary = await _expenseService.GetGoalsSummaryAsync(memberGlobalId);
        var allGoals = await _expenseService.GetGoalsAsync(memberGlobalId);
        var recurringSummary = await _expenseService.GetRecurringSummaryAsync(memberGlobalId);
        var allRecurring = await _expenseService.GetRecurringTransactionsAsync(memberGlobalId);
        var loansSummary = await _expenseService.GetLoansSummaryAsync(memberGlobalId);
        var (recentTx, totalTxCount, _) = await _expenseService.GetExpensesAsync(1, 100, memberGlobalId: memberGlobalId);

        // 3. Runway Calculation — short-circuit to avoid nonsensical result when no expenses recorded
        double runwayMonths;
        if (kpis.CurrentMonthExpense <= 0)
        {
            // No expenses: runway is effectively unlimited; cap display at 999 months
            runwayMonths = kpis.TotalNetBalance > 0 ? 999.0 : 0.0;
        }
        else
        {
            runwayMonths = kpis.TotalNetBalance > 0
                ? (double)Math.Round(kpis.TotalNetBalance / kpis.CurrentMonthExpense, 1)
                : 0.0;
        }

        // 4. 50/30/20 Rule Computation
        var split = Compute50_30_20(categoryTotals, kpis.CurrentMonthIncome, kpis.CurrentMonthExpense, goalsSummary.TotalSaved);

        // 5. Goal Feasibility Assessments
        var goalAssessments = EvaluateGoals(allGoals, kpis.NetMonthlyCashFlow);

        // 6. Subscriptions Audit
        var subAudits = AuditSubscriptions(allRecurring, kpis.CurrentMonthIncome);

        // 7. Calculate Health Scores (0 to 20 for each category)
        int savingsScore = CalculateSavingsScore(kpis.SavingsRate, kpis.NetMonthlyCashFlow, kpis.CurrentMonthIncome);
        int runwayScore = CalculateRunwayScore(runwayMonths, kpis.TotalNetBalance);
        int budgetScore = CalculateBudgetControlScore(budgetStatuses, split);
        int goalsScore = CalculateGoalsScore(goalsSummary, goalAssessments);
        int fixedScore = CalculateFixedCommitmentsScore(recurringSummary, kpis.CurrentMonthIncome);

        int overallHealthScore = Math.Clamp(savingsScore + runwayScore + budgetScore + goalsScore + fixedScore, 5, 100);

        var (tier, color, tagline) = ResolveHealthTier(overallHealthScore);

        // 8. Generate Insights & Actionable Recommendations
        var (wins, vulnerabilities, recommendations) = GenerateInsightsAndAdvice(
            profileName,
            kpis,
            split,
            budgetStatuses,
            goalAssessments,
            subAudits,
            loansSummary,
            runwayMonths,
            overallHealthScore
        );

        // 9. Generate Executive Diagnostic Summary
        string executiveSummary = GenerateExecutiveSummary(
            profileName,
            tier,
            overallHealthScore,
            kpis,
            runwayMonths,
            goalAssessments,
            recommendations
        );

        // Optional: If Gemini API key is configured, enrich the executive summary
        if (!string.IsNullOrWhiteSpace(geminiApiKey))
        {
            try
            {
                var aiSummary = await TryEnrichWithGeminiAsync(
                    geminiApiKey,
                    profileName,
                    kpis,
                    runwayMonths,
                    overallHealthScore,
                    tier,
                    recommendations
                );
                if (!string.IsNullOrWhiteSpace(aiSummary))
                {
                    executiveSummary = aiSummary;
                }
            }
            catch (Exception ex)
            {
                // Log the failure but silently fallback to built-in deterministic summary
                Console.Error.WriteLine($"[AiAdvisor] Gemini enrichment failed: {ex.GetType().Name}: {ex.Message}");
            }
        }

        return new AiAnalysisReport(
            Profile: profile,
            IsFamilyWide: isFamilyWide,
            ProfileDisplayName: profileName,
            OverallHealthScore: overallHealthScore,
            HealthTier: tier,
            HealthColor: color,
            HealthTagline: tagline,
            SavingsScore: savingsScore,
            RunwayScore: runwayScore,
            BudgetControlScore: budgetScore,
            GoalsProgressScore: goalsScore,
            FixedCommitmentsScore: fixedScore,
            TotalNetBalance: kpis.TotalNetBalance,
            CurrentMonthIncome: kpis.CurrentMonthIncome,
            CurrentMonthExpense: kpis.CurrentMonthExpense,
            NetCashFlow: kpis.NetMonthlyCashFlow,
            SavingsRatePct: Math.Round(kpis.SavingsRate, 1),
            DailyBurnRate: Math.Round(kpis.DailyBurnRate, 2),
            CashRunwayMonths: runwayMonths,
            Rule50_30_20: split,
            KeyWins: wins,
            Vulnerabilities: vulnerabilities,
            Recommendations: recommendations,
            GoalAssessments: goalAssessments,
            SubscriptionAudits: subAudits,
            ExecutiveSummary: executiveSummary,
            GeneratedAt: DateTime.Now
        );
    }

    #endregion

    #region Heuristic Scoring & Rule Engines

    private static int CalculateSavingsScore(double savingsRate, decimal netCashFlow, decimal income)
    {
        if (income <= 0 && netCashFlow <= 0) return 6;
        if (savingsRate >= 30.0) return 20;
        if (savingsRate >= 20.0) return 17;
        if (savingsRate >= 10.0) return 13;
        if (savingsRate >= 0.0) return 9;
        if (savingsRate >= -15.0) return 4;
        return 1;
    }

    private static int CalculateRunwayScore(double runwayMonths, decimal netBalance)
    {
        if (netBalance < 0) return 0;
        if (runwayMonths >= 6.0) return 20;
        if (runwayMonths >= 4.0) return 17;
        if (runwayMonths >= 2.5) return 14;
        if (runwayMonths >= 1.0) return 10;
        if (runwayMonths >= 0.5) return 6;
        return 3;
    }

    private static int CalculateBudgetControlScore(List<ExpenseService.CategoryBudgetStatus> budgets, BudgetRuleSplit split)
    {
        if (budgets.Count == 0)
        {
            // Fallback to wants vs needs balance
            if (split.WantsPct <= 35.0) return 16;
            if (split.WantsPct <= 45.0) return 12;
            return 8;
        }

        int overCount = budgets.Count(b => b.IsOverBudget);
        int nearCount = budgets.Count(b => b.IsNearLimit);

        if (overCount == 0 && nearCount == 0) return 20;
        if (overCount == 0) return 16;
        if (overCount == 1) return 12;
        if (overCount == 2) return 8;
        return 4;
    }

    private static int CalculateGoalsScore(ExpenseService.GoalsSummary summary, List<GoalFeasibilityAssessment> assessments)
    {
        if (summary.TotalGoals == 0) return 10; // Neutral baseline

        double onTrackPct = assessments.Count > 0
            ? (double)assessments.Count(a => a.FeasibilityStatus == "On Track" || a.FeasibilityStatus == "Completed") / assessments.Count * 100.0
            : 0;

        if (onTrackPct >= 75.0) return 20;
        if (onTrackPct >= 50.0) return 16;
        if (summary.OverallProgressPct >= 40.0) return 13;
        if (summary.OverallProgressPct >= 15.0) return 9;
        return 5;
    }

    private static int CalculateFixedCommitmentsScore(ExpenseService.RecurringSummary recurring, decimal monthlyIncome)
    {
        if (monthlyIncome <= 0) return 12;

        decimal monthlyLoad = recurring.MonthlyEquivalentExpense;
        double loadRatio = (double)(monthlyLoad / monthlyIncome * 100m);

        int baseScore = 20;
        if (loadRatio > 40.0) baseScore = 6;
        else if (loadRatio > 25.0) baseScore = 11;
        else if (loadRatio > 15.0) baseScore = 15;
        else baseScore = 20;

        if (recurring.OverdueCount > 0) baseScore = Math.Max(2, baseScore - 6);
        return baseScore;
    }

    private static (string Tier, string Color, string Tagline) ResolveHealthTier(int score)
    {
        return score switch
        {
            >= 85 => ("Excellent", "#10b981", "Financially resilient & primed for wealth generation"),
            >= 70 => ("Healthy", "#06b6d4", "Solid financial fundamentals with strong positive trajectory"),
            >= 55 => ("Fair", "#f59e0b", "Stable foundation, but vulnerable to unexpected expenses"),
            >= 40 => ("Needs Attention", "#f97316", "High burn rate with compressed savings margins"),
            _     => ("Critical", "#f43f5e", "High financial stress — urgent restructuring recommended")
        };
    }

    private static BudgetRuleSplit Compute50_30_20(
        Dictionary<string, decimal> categoryTotals,
        decimal income,
        decimal totalExpense,
        decimal savedInGoals)
    {
        // Broad keyword list — uses partial matching so "grocery", "Groceries", "food" all match as needs
        var needsKeywords = new[]
        {
            "grocer", "food", "dining", "restaurant", "meal", "utility", "utilities",
            "rent", "housing", "mortgage", "health", "medical", "doctor", "hospital",
            "transport", "travel", "fuel", "petrol", "bills", "electricity", "water",
            "maintenance", "repair", "education", "school", "tuition", "insurance",
            "internet", "mobile", "phone", "gas", "emi", "loan"
        };

        decimal needsTotal = 0;
        decimal wantsTotal = 0;

        foreach (var (catName, amount) in categoryTotals)
        {
            var catLower = catName.ToLowerInvariant();
            // Skip income/transfer pseudo-categories that may have crept in
            if (catLower == "uncategorised" || catLower.Contains("transfer") || catLower.Contains("income"))
                continue;

            if (needsKeywords.Any(keyword => catLower.Contains(keyword)))
                needsTotal += amount;
            else
                wantsTotal += amount;
        }

        decimal effectiveIncome = income > 0 ? income : Math.Max(1m, totalExpense);
        double needsPct = (double)Math.Round(needsTotal / effectiveIncome * 100m, 1);
        double wantsPct = (double)Math.Round(wantsTotal / effectiveIncome * 100m, 1);
        decimal savingsAmount = Math.Max(0m, effectiveIncome - totalExpense);
        double savingsPct = (double)Math.Round(savingsAmount / effectiveIncome * 100m, 1);

        string verdict;
        if (needsPct <= 55 && wantsPct <= 35 && savingsPct >= 15)
            verdict = "Balanced! Following standard 50/30/20 guidelines.";
        else if (wantsPct > 35)
            verdict = $"Wants spending is elevated ({wantsPct:F0}% vs 30% target). Look to trim discretionary costs.";
        else if (needsPct > 65)
            verdict = $"Essential needs consume {needsPct:F0}% of income. High fixed cost pressure.";
        else
            verdict = "Savings rate can be optimized by redirecting unspent cash flow into target funds.";

        return new BudgetRuleSplit(needsTotal, needsPct, wantsTotal, wantsPct, savingsAmount, savingsPct, verdict);
    }

    private static List<GoalFeasibilityAssessment> EvaluateGoals(List<FinancialGoal> goals, decimal netMonthlyCashFlow)
    {
        var results = new List<GoalFeasibilityAssessment>();
        var today = DateTime.Today;

        foreach (var goal in goals)
        {
            var remaining = Math.Max(0m, goal.TargetAmount - goal.CurrentAmount);
            double pct = goal.TargetAmount > 0 ? (double)(goal.CurrentAmount / goal.TargetAmount * 100m) : 0;
            pct = Math.Min(100.0, Math.Round(pct, 1));

            if (goal.Status == "Completed" || remaining == 0)
            {
                results.Add(new GoalFeasibilityAssessment(
                    goal.GlobalId,
                    goal.Name,
                    goal.Icon ?? "🎯",
                    goal.Colour ?? "#10b981",
                    goal.TargetAmount,
                    goal.CurrentAmount,
                    100.0,
                    goal.TargetDate,
                    0,
                    0,
                    "Completed",
                    "Goal target reached! Consider rolling surplus cash flow into your next milestone."
                ));
                continue;
            }

            // Estimate required monthly allocation
            decimal reqMonthly = 0;
            string status = "On Track";
            string advice = "";

            if (goal.TargetDate.HasValue && goal.TargetDate.Value > today)
            {
                int monthsLeft = Math.Max(1, ((goal.TargetDate.Value.Year - today.Year) * 12) + goal.TargetDate.Value.Month - today.Month);
                reqMonthly = Math.Round(remaining / monthsLeft, 2);

                if (netMonthlyCashFlow <= 0)
                {
                    status = "At Risk";
                    advice = $"Zero positive cash flow. Requires ₹{reqMonthly:N0}/mo over the next {monthsLeft} months to achieve on time.";
                }
                else if (reqMonthly > netMonthlyCashFlow)
                {
                    status = "Needs Acceleration";
                    advice = $"Required ₹{reqMonthly:N0}/mo exceeds your current monthly surplus (₹{netMonthlyCashFlow:N0}). Extend target date or boost savings.";
                }
                else
                {
                    status = "On Track";
                    advice = $"Easily achievable! Allocate ₹{reqMonthly:N0}/mo from your current surplus of ₹{netMonthlyCashFlow:N0}.";
                }
            }
            else if (goal.TargetDate.HasValue && goal.TargetDate.Value <= today)
            {
                status = "At Risk";
                reqMonthly = remaining;
                advice = $"Target date was {goal.TargetDate.Value:dd MMM yyyy}. Still needs ₹{remaining:N0} to complete.";
            }
            else
            {
                // Open-ended goal without target date
                status = goal.CurrentAmount > 0 ? "On Track" : "Needs Acceleration";
                reqMonthly = remaining > 0 ? Math.Round(remaining / 12m, 2) : 0;
                advice = $"Open-ended timeline. Contributing ₹{reqMonthly:N0}/mo will complete this goal in 1 year.";
            }

            results.Add(new GoalFeasibilityAssessment(
                goal.GlobalId,
                goal.Name,
                goal.Icon ?? "🎯",
                goal.Colour ?? "#6366f1",
                goal.TargetAmount,
                goal.CurrentAmount,
                pct,
                goal.TargetDate,
                0,
                reqMonthly,
                status,
                advice
            ));
        }

        return results;
    }

    private static List<SubscriptionAuditItem> AuditSubscriptions(List<RecurringTransaction> items, decimal monthlyIncome)
    {
        var audits = new List<SubscriptionAuditItem>();

        foreach (var item in items.Where(i => i.Status == "Active" && i.Type == "Expense"))
        {
            var monthly = ExpenseService.CalculateMonthlyEquivalent(item.Amount, item.Frequency);
            double ratio = monthlyIncome > 0 ? (double)Math.Round(monthly / monthlyIncome * 100m, 1) : 0;

            string assessment = ratio >= 5.0
                ? $"Consumes {ratio:F1}% of monthly income. Significant recurring cost."
                : $"Healthy low impact ({monthly:C0}/mo).";

            audits.Add(new SubscriptionAuditItem(
                item.Name,
                item.Frequency,
                item.Amount,
                monthly,
                ratio,
                item.Status,
                assessment
            ));
        }

        return audits.OrderByDescending(a => a.MonthlyCost).ToList();
    }

    private static (List<AiInsightItem> Wins, List<AiInsightItem> Vulnerabilities, List<AiRecommendation> Recommendations) GenerateInsightsAndAdvice(
        string profileName,
        ExpenseService.FinancialKpiSummary kpis,
        BudgetRuleSplit split,
        List<ExpenseService.CategoryBudgetStatus> budgets,
        List<GoalFeasibilityAssessment> goals,
        List<SubscriptionAuditItem> subs,
        ExpenseService.LoansSummary loans,
        double runwayMonths,
        int healthScore)
    {
        var wins = new List<AiInsightItem>();
        var vulns = new List<AiInsightItem>();
        var recs = new List<AiRecommendation>();

        // 1. Evaluate Net Cash Flow & Savings
        if (kpis.NetMonthlyCashFlow > 0 && kpis.SavingsRate >= 20)
        {
            wins.Add(new AiInsightItem("📈", "Outstanding Savings Rate",
                $"{profileName} is saving {kpis.SavingsRate:F1}% of monthly income (₹{kpis.NetMonthlyCashFlow:N2} retained). Above the 20% benchmark.", "success"));
        }
        else if (kpis.NetMonthlyCashFlow < 0)
        {
            vulns.Add(new AiInsightItem("🚨", "Negative Monthly Cash Flow",
                $"Outflows exceed income by ₹{Math.Abs(kpis.NetMonthlyCashFlow):N2} this month. Capital reserves are actively depleting.", "danger"));

            recs.Add(new AiRecommendation(
                "rec-cashflow",
                "Spending",
                "High",
                "🛑",
                "Plug Cash Flow Deficit",
                $"Reduce non-essential discretionary expenses by ₹{Math.Abs(kpis.NetMonthlyCashFlow):N0} to break even and avoid debt accumulation.",
                Math.Abs(kpis.NetMonthlyCashFlow),
                "/expenses",
                "Review Transactions"
            ));
        }

        // 2. Evaluate Cash Runway
        if (runwayMonths >= 4.0)
        {
            wins.Add(new AiInsightItem("🛡️", "Robust Emergency Cushion",
                $"Liquid accounts cover {runwayMonths:F1} months of average burn (₹{kpis.TotalNetBalance:N0}). High shock resilience.", "success"));
        }
        else if (runwayMonths < 2.0 && kpis.TotalNetBalance >= 0)
        {
            vulns.Add(new AiInsightItem("⚠️", "Thin Liquidity Buffer",
                $"Current liquid balance covers only {runwayMonths:F1} months of expenses. An unexpected shock could cause distress.", "warning"));

            recs.Add(new AiRecommendation(
                "rec-runway",
                "Safety",
                "High",
                "🛡️",
                "Build 3-Month Emergency Fund",
                $"Aim for a liquid reserve of at least ₹{(kpis.CurrentMonthExpense * 3):N0}. Set up an automated savings sinking fund.",
                kpis.CurrentMonthExpense * 0.15m,
                "/goals",
                "Create Emergency Goal"
            ));
        }
        else if (kpis.TotalNetBalance < 0)
        {
            vulns.Add(new AiInsightItem("🚨", "Overdrawn Accounts Detected",
                $"Net balance is negative (₹{kpis.TotalNetBalance:N2}). Overdraft interest or penalties may apply.", "danger"));
        }

        // 3. Category Budgets
        var overBudgets = budgets.Where(b => b.IsOverBudget).ToList();
        if (overBudgets.Any())
        {
            foreach (var ob in overBudgets.Take(2))
            {
                vulns.Add(new AiInsightItem("💸", $"{ob.CategoryName} Budget Exceeded",
                    $"Spent ₹{ob.Spent:N0} of ₹{ob.Budget!.Value:N0} (+₹{(ob.Spent - ob.Budget.Value):N0}).", "danger"));

                recs.Add(new AiRecommendation(
                    $"rec-cat-{ob.CategoryName}",
                    "Spending",
                    "Medium",
                    "✂️",
                    $"Trim {ob.CategoryName} Outflow",
                    $"Re-align spending in {ob.CategoryName} back to ₹{ob.Budget!.Value:N0} to preserve ₹{(ob.Spent - ob.Budget.Value):N0} monthly.",
                    ob.Spent - ob.Budget.Value,
                    "/categories",
                    "Adjust Budgets"
                ));
            }
        }
        else if (budgets.Any())
        {
            wins.Add(new AiInsightItem("🎯", "Disciplined Budget Adherence",
                "All categorized limits are respected with zero budget overruns this month.", "success"));
        }

        // 4. Goals Assessment
        var behindGoals = goals.Where(g => g.FeasibilityStatus == "Needs Acceleration" || g.FeasibilityStatus == "At Risk").ToList();
        if (behindGoals.Any())
        {
            var topBehind = behindGoals.First();
            recs.Add(new AiRecommendation(
                $"rec-goal-{topBehind.GoalName}",
                "Goals",
                "High",
                "🎯",
                $"Accelerate '{topBehind.GoalName}'",
                $"Allocate ₹{topBehind.RequiredMonthlyToTarget:N0}/month from upcoming cash flow to hit your target by {topBehind.TargetDate:MMM yyyy}.",
                topBehind.RequiredMonthlyToTarget,
                "/goals",
                "Deposit Funds"
            ));
        }

        var completedGoals = goals.Where(g => g.FeasibilityStatus == "Completed").ToList();
        if (completedGoals.Any())
        {
            wins.Add(new AiInsightItem("🎉", "Milestone Reached",
                $"Congratulations! '{completedGoals.First().GoalName}' has reached 100% funding.", "success"));
        }

        // 5. Subscriptions Load
        decimal totalMonthlySubs = subs.Sum(s => s.MonthlyCost);
        if (subs.Count > 0 && totalMonthlySubs > 0)
        {
            if (subs.Any(s => s.IncomeRatioPct > 5.0))
            {
                var heavySub = subs.First(s => s.IncomeRatioPct > 5.0);
                vulns.Add(new AiInsightItem("📱", $"High-Cost Subscription: {heavySub.Name}",
                    $"{heavySub.Name} costs ₹{heavySub.MonthlyCost:N0}/mo ({heavySub.IncomeRatioPct:F1}% of your total earnings).", "warning"));

                recs.Add(new AiRecommendation(
                    $"rec-sub-{heavySub.Name}",
                    "Subscriptions",
                    "Opportunity",
                    "🔁",
                    $"Audit {heavySub.Name} Subscription",
                    $"Consider switching to an annual plan or sharing a family tier to reduce monthly overhead.",
                    heavySub.MonthlyCost * 0.25m,
                    "/subscriptions",
                    "Manage Subscriptions"
                ));
            }
        }

        // 6. Loans & Debt Service Burden (DTI)
        if (loans.ActiveLoansCount > 0)
        {
            double dti = kpis.CurrentMonthIncome > 0
                ? (double)(loans.TotalMonthlyEmi / kpis.CurrentMonthIncome * 100m)
                : 0;

            if (dti > 40.0)
            {
                vulns.Add(new AiInsightItem("💳", $"High Debt-to-Income Strain ({dti:F1}%)",
                    $"Monthly EMIs (₹{loans.TotalMonthlyEmi:N0}/mo) consume {dti:F1}% of income across {loans.ActiveLoansCount} active loans.", "warning"));

                recs.Add(new AiRecommendation(
                    "rec-debt-prepay",
                    "Debt",
                    "High",
                    "💳",
                    "Target High-Interest Loan Prepayments",
                    $"Make extra lump-sum part-payments on your loans to reduce tenure and interest obligations.",
                    loans.TotalMonthlyEmi * 0.15m,
                    "/loans",
                    "View Loans"
                ));
            }
            else if (dti > 0 && dti <= 25.0)
            {
                wins.Add(new AiInsightItem("💳", "Healthy Debt-to-Income Ratio",
                    $"Active loan EMIs represent {dti:F1}% of income (₹{loans.TotalMonthlyEmi:N0}/mo). Well within safe limits.", "success"));
            }

            if (loans.OverdueCount > 0)
            {
                vulns.Add(new AiInsightItem("🚨", "Overdue Loan Installments",
                    $"You have {loans.OverdueCount} overdue EMI(s) totaling ₹{loans.OverdueAmount:N2}. Clear immediately to prevent penalties.", "danger"));
            }
        }

        // 6. Generic Wealth Opportunity if doing well
        if (healthScore >= 75 && kpis.NetMonthlyCashFlow > 10000)
        {
            recs.Add(new AiRecommendation(
                "rec-invest-surplus",
                "Savings",
                "Opportunity",
                "💎",
                "Deploy Idle Surplus into Sinking Funds or SIP",
                $"You have a strong monthly buffer of ₹{kpis.NetMonthlyCashFlow:N0}. Direct ₹{(kpis.NetMonthlyCashFlow * 0.5m):N0} into wealth-building goals or emergency reserves.",
                kpis.NetMonthlyCashFlow * 0.5m,
                "/goals",
                "Allocate Surplus"
            ));
        }

        return (wins, vulns, recs);
    }

    private static string GenerateExecutiveSummary(
        string profileName,
        string tier,
        int score,
        ExpenseService.FinancialKpiSummary kpis,
        double runwayMonths,
        List<GoalFeasibilityAssessment> goals,
        List<AiRecommendation> recs)
    {
        var sb = new StringBuilder();

        sb.AppendLine($"### 🤖 Financial Diagnostic for **{profileName}**");
        sb.AppendLine();
        sb.AppendLine($"Your overall Financial Health Index is rated **{score}/100 ({tier})**. ");

        if (kpis.NetMonthlyCashFlow >= 0)
        {
            sb.AppendLine($"This month, you generated **₹{kpis.CurrentMonthIncome:N2}** in total inflows while maintaining outflows at **₹{kpis.CurrentMonthExpense:N2}**, resulting in a positive cash flow of **+₹{kpis.NetMonthlyCashFlow:N2}** (savings rate: **{kpis.SavingsRate:F1}%**). ");
        }
        else
        {
            sb.AppendLine($"This month, your outflows (**₹{kpis.CurrentMonthExpense:N2}**) exceeded your inflows (**₹{kpis.CurrentMonthIncome:N2}**) by **-₹{Math.Abs(kpis.NetMonthlyCashFlow):N2}**. Immediate budget moderation is recommended to prevent balance erosion. ");
        }

        if (runwayMonths >= 3.0)
        {
            sb.AppendLine($"Your current liquid net balance of **₹{kpis.TotalNetBalance:N2}** provides a secure cushion of **{runwayMonths:F1} months** of essential living expenses. ");
        }
        else
        {
            sb.AppendLine($"Your liquid runway currently stands at **{runwayMonths:F1} months**, which leaves little room for unexpected emergencies. Prioritize building an emergency sinking fund. ");
        }

        var activeGoals = goals.Where(g => g.FeasibilityStatus != "Completed").ToList();
        if (activeGoals.Any())
        {
            sb.AppendLine($"You have **{activeGoals.Count} active financial goals**. Focusing consistent monthly contributions will significantly accelerate your completion timelines.");
        }

        if (recs.Any())
        {
            sb.AppendLine();
            sb.AppendLine($"**Top Action Item:** {recs.First().Title} — {recs.First().Description}");
        }

        return sb.ToString();
    }

    #endregion

    #region Live Gemini API Integration (Optional / Fallback)

    private async Task<string?> TryEnrichWithGeminiAsync(
        string apiKey,
        string profileName,
        ExpenseService.FinancialKpiSummary kpis,
        double runwayMonths,
        int healthScore,
        string tier,
        List<AiRecommendation> recs)
    {
        var url = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-flash:generateContent?key={apiKey}";

        var prompt = $@"
You are an expert personal financial advisor and wealth coach.
Analyze the following financial snapshot for user '{profileName}' and write a concise, encouraging, highly professional 2-paragraph financial diagnosis and top 3 practical tips.

Snapshot:
- Financial Health Score: {healthScore}/100 ({tier})
- Total Net Balance: ₹{kpis.TotalNetBalance:N2}
- Current Month Inflows: ₹{kpis.CurrentMonthIncome:N2}
- Current Month Outflows: ₹{kpis.CurrentMonthExpense:N2}
- Net Monthly Cash Flow: ₹{kpis.NetMonthlyCashFlow:N2} (Savings Rate: {kpis.SavingsRate:F1}%)
- Daily Burn Rate: ₹{kpis.DailyBurnRate:N2}/day
- Liquid Runway: {runwayMonths:F1} months
- Key Suggested Action: {recs.FirstOrDefault()?.Title}

Output format: Return clean Markdown with bold numbers. Be polite, direct, and constructive. Keep it under 200 words.
";

        var payload = new
        {
            contents = new[]
            {
                new
                {
                    parts = new[] { new { text = prompt } }
                }
            }
        };

        var json = JsonSerializer.Serialize(payload);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(8));
        var response = await _httpClient.PostAsync(url, content, cts.Token);

        if (!response.IsSuccessStatusCode) return null;

        var respStr = await response.Content.ReadAsStringAsync(cts.Token);
        using var doc = JsonDocument.Parse(respStr);

        var root = doc.RootElement;
        if (root.TryGetProperty("candidates", out var candidates) && candidates.GetArrayLength() > 0)
        {
            var first = candidates[0];
            if (first.TryGetProperty("content", out var cObj) &&
                cObj.TryGetProperty("parts", out var parts) &&
                parts.GetArrayLength() > 0)
            {
                return parts[0].GetProperty("text").GetString();
            }
        }

        return null;
    }

    #endregion

    #region Interactive Q&A ("Ask the AI Financial Advisor")

    public async Task<AiAdviceReply> AskAdvisorAsync(AiAnalysisReport report, string userQuestion, string? geminiApiKey = null)
    {
        if (string.IsNullOrWhiteSpace(userQuestion))
        {
            return new AiAdviceReply("Please ask a financial question, such as 'Can I afford a new phone?' or 'How can I save ₹10,000 this month?'.", [], false);
        }

        // Try Gemini live LLM call first if API key provided
        if (!string.IsNullOrWhiteSpace(geminiApiKey))
        {
            try
            {
                var geminiReply = await AskGeminiLiveAsync(report, userQuestion, geminiApiKey);
                if (geminiReply != null) return geminiReply;
            }
            catch (Exception ex)
            {
                // Log the failure but fall back to rule-based financial reasoning engine
                Console.Error.WriteLine($"[AiAdvisor] Gemini live call failed: {ex.GetType().Name}: {ex.Message}");
            }
        }

        // Built-in Deterministic Financial Intelligence Engine
        return AnswerWithDeterministicEngine(report, userQuestion);
    }

    private async Task<AiAdviceReply?> AskGeminiLiveAsync(AiAnalysisReport report, string question, string apiKey)
    {
        var url = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-flash:generateContent?key={apiKey}";

        var prompt = $@"
You are 'FalimyCalc AI Advisor', an empathetic, razor-sharp personal finance AI assistant.
User Profile: '{report.ProfileDisplayName}'

User Financial Reality:
- Total Net Liquid Balance: ₹{report.TotalNetBalance:N2}
- Monthly Income: ₹{report.CurrentMonthIncome:N2}
- Monthly Spend: ₹{report.CurrentMonthExpense:N2}
- Monthly Net Cash Flow: ₹{report.NetCashFlow:N2} (Savings Rate: {report.SavingsRatePct:F1}%)
- Daily Burn: ₹{report.DailyBurnRate:N2}/day
- Liquid Runway: {report.CashRunwayMonths:F1} months
- Needs: ₹{report.Rule50_30_20.NeedsAmount:N0} ({report.Rule50_30_20.NeedsPct:F0}%), Wants: ₹{report.Rule50_30_20.WantsAmount:N0} ({report.Rule50_30_20.WantsPct:F0}%)
- Active Goals: {string.Join(", ", report.GoalAssessments.Select(g => $"{g.GoalName} ({g.ProgressPct:F0}% funded, target ₹{g.TargetAmount:N0})"))}

User Question: ""{question}""

Instructions:
- Provide an exact, mathematically sound answer based on the numbers above.
- Mention specific rupee amounts and percentages where relevant.
- Keep the response concise (2 to 3 paragraphs max), encouraging yet realistic.
- Suggest 2 short follow-up questions at the very end formatted as:
FOLLOW_UP: Question 1
FOLLOW_UP: Question 2
";

        var payload = new
        {
            contents = new[]
            {
                new { parts = new[] { new { text = prompt } } }
            }
        };

        var json = JsonSerializer.Serialize(payload);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var response = await _httpClient.PostAsync(url, content, cts.Token);
        if (!response.IsSuccessStatusCode) return null;

        var respStr = await response.Content.ReadAsStringAsync(cts.Token);
        using var doc = JsonDocument.Parse(respStr);

        var root = doc.RootElement;
        if (root.TryGetProperty("candidates", out var candidates) && candidates.GetArrayLength() > 0)
        {
            var first = candidates[0];
            if (first.TryGetProperty("content", out var cObj) &&
                cObj.TryGetProperty("parts", out var parts) &&
                parts.GetArrayLength() > 0)
            {
                var fullText = parts[0].GetProperty("text").GetString() ?? "";
                var followUps = new List<string>();

                var lines = fullText.Split('\n');
                var cleanedLines = new List<string>();
                foreach (var line in lines)
                {
                    if (line.StartsWith("FOLLOW_UP:", StringComparison.OrdinalIgnoreCase))
                    {
                        var q = line.Substring("FOLLOW_UP:".Length).Trim();
                        if (!string.IsNullOrEmpty(q)) followUps.Add(q);
                    }
                    else
                    {
                        cleanedLines.Add(line);
                    }
                }

                return new AiAdviceReply(string.Join("\n", cleanedLines).Trim(), followUps, true);
            }
        }

        return null;
    }

    private static AiAdviceReply AnswerWithDeterministicEngine(AiAnalysisReport report, string question)
    {
        var q = question.ToLowerInvariant();
        var sb = new StringBuilder();
        var followUps = new List<string>();

        // 1. "Can I afford" or purchase inquiries
        if (q.Contains("afford") || q.Contains("buy") || q.Contains("purchase") || q.Contains("cost"))
        {
            decimal extractedAmount = ExtractRupees(question);
            if (extractedAmount == 0) extractedAmount = 25000m; // Default hypothetical purchase

            sb.AppendLine($"### 🔍 Affordability Evaluation for ₹{extractedAmount:N2}");
            sb.AppendLine();

            decimal safeLiquidSurplus = Math.Max(0m, report.TotalNetBalance - (report.CurrentMonthExpense * 2m));

            if (report.TotalNetBalance < extractedAmount)
            {
                sb.AppendLine($"❌ **Not recommended right now.** Your total net balance across all accounts is **₹{report.TotalNetBalance:N2}**, which is less than the requested amount of **₹{extractedAmount:N2}**.");
                sb.AppendLine("Making this purchase now would push your accounts into negative territory or deplete all reserves.");
            }
            else if (extractedAmount <= safeLiquidSurplus && report.NetCashFlow > 0)
            {
                sb.AppendLine($"✅ **Yes, you can comfortably afford this!**");
                var remainingBalance = report.TotalNetBalance - extractedAmount;
                var runwayAfter = report.CurrentMonthExpense > 0
                    ? (remainingBalance / report.CurrentMonthExpense).ToString("F1") + " months"
                    : "∞ (no expenses recorded)";
                sb.AppendLine($"Even after spending **₹{extractedAmount:N2}**, your remaining balance (**₹{remainingBalance:N2}**) maintains a safe **{runwayAfter}** emergency runway.");
                sb.AppendLine($"Additionally, your positive monthly surplus of **+₹{report.NetCashFlow:N2}** will replenish this within **{Math.Ceiling(extractedAmount / Math.Max(1m, report.NetCashFlow))} month(s)**.");
            }
            else
            {
                sb.AppendLine($"⚠️ **Proceed with caution.** While you technically have **₹{report.TotalNetBalance:N2}** in your accounts, spending **₹{extractedAmount:N2}** would reduce your emergency buffer to **{(report.CurrentMonthExpense > 0 ? ((report.TotalNetBalance - extractedAmount) / report.CurrentMonthExpense).ToString("F1") + " months" : "∞")}**.");
                sb.AppendLine($"Consider setting up a dedicated **Sinking Fund Goal** of ₹{extractedAmount / 3m:N0}/month over 3 months to purchase it stress-free.");
            }

            followUps.Add("How long will my emergency fund last?");
            followUps.Add("How can I increase my monthly savings?");
        }
        // 2. Emergency fund inquiries
        else if (q.Contains("emergency") || q.Contains("runway") || q.Contains("reserve") || q.Contains("cushion"))
        {
            sb.AppendLine("### 🛡️ Emergency Reserve & Runway Analysis");
            sb.AppendLine();
            sb.AppendLine($"Your current total liquid balance is **₹{report.TotalNetBalance:N2}**.");
            sb.AppendLine($"Based on your current monthly outflow of **₹{report.CurrentMonthExpense:N2}**, your reserves will last **{report.CashRunwayMonths:F1} months** without any income.");
            sb.AppendLine();

            if (report.CashRunwayMonths >= 6.0)
            {
                sb.AppendLine("🌟 **Gold Standard!** You have more than 6 months of living expenses covered. Your liquid capital provides superb protection.");
            }
            else if (report.CashRunwayMonths >= 3.0)
            {
                sb.AppendLine("👍 **Solid Base.** You have adequate 3-month protection. Aim to gradually build toward 6 months.");
            }
            else
            {
                decimal shortfall = (report.CurrentMonthExpense * 3m) - report.TotalNetBalance;
                sb.AppendLine($"⚠️ **Vulnerable.** Standard financial planning recommends keeping at least 3 months (₹{(report.CurrentMonthExpense * 3m):N2}). You have a shortfall of **₹{shortfall:N2}**.");
            }

            followUps.Add("Which category can I cut to save more?");
            followUps.Add("Can I afford a ₹15,000 purchase?");
        }
        // 3. How to save more or trim expenses
        else if (q.Contains("save") || q.Contains("trim") || q.Contains("cut") || q.Contains("reduce"))
        {
            sb.AppendLine("### 💡 Strategic Savings Blueprint");
            sb.AppendLine();
            sb.AppendLine($"Your current savings rate is **{report.SavingsRatePct:F1}%** (+₹{report.NetCashFlow:N2}/month).");
            sb.AppendLine();
            sb.AppendLine("Here are 3 concrete moves to increase your cash retention:");

            if (report.Rule50_30_20.WantsAmount > 0)
            {
                sb.AppendLine($"1. **Discretionary Trim (Wants):** You spent **₹{report.Rule50_30_20.WantsAmount:N2}** on non-essential categories ({report.Rule50_30_20.WantsPct:F0}% of income). Trimming just 15% frees up **₹{(report.Rule50_30_20.WantsAmount * 0.15m):N0}/month**.");
            }

            if (report.SubscriptionAudits.Any())
            {
                var topSub = report.SubscriptionAudits.First();
                sb.AppendLine($"2. **Subscription Audit:** You have **{report.SubscriptionAudits.Count} active subscriptions** totaling **₹{report.SubscriptionAudits.Sum(s => s.MonthlyCost):N0}/mo**. Auditing `{topSub.Name}` (₹{topSub.MonthlyCost:N0}/mo) can yield immediate savings.");
            }

            sb.AppendLine("3. **Automate Pay-Yourself-First:** On salary day, automatically transfer 20% to your Sinking Fund before paying discretionary bills.");

            followUps.Add("Am I on track for my financial goals?");
            followUps.Add("How long will my emergency fund last?");
        }
        // 4. Goals trajectory
        else if (q.Contains("goal") || q.Contains("target") || q.Contains("sinking fund"))
        {
            sb.AppendLine("### 🎯 Financial Goals Trajectory");
            sb.AppendLine();

            if (!report.GoalAssessments.Any())
            {
                sb.AppendLine("You do not have any active financial goals set up yet.");
                sb.AppendLine("Setting up specific goals (e.g. Vacation, Emergency Fund, Home Down Payment) increases your savings rate by over 30%!");
            }
            else
            {
                sb.AppendLine($"You currently have **{report.GoalAssessments.Count} tracked goals**:");
                foreach (var g in report.GoalAssessments)
                {
                    sb.AppendLine($"- **{g.Icon} {g.GoalName}**: ₹{g.CurrentAmount:N0} of ₹{g.TargetAmount:N0} ({g.ProgressPct:F0}%) — Status: **{g.FeasibilityStatus}**.");
                    sb.AppendLine($"  *Advice:* {g.Advice}");
                }
            }

            followUps.Add("How can I increase my monthly savings?");
            followUps.Add("Can I afford a ₹30,000 purchase?");
        }
        // 5. Loans, EMIs, and debt inquiries
        else if (q.Contains("loan") || q.Contains("emi") || q.Contains("debt") || q.Contains("prepay") || q.Contains("interest"))
        {
            sb.AppendLine("### 💳 Loans, EMIs & Debt Payoff Strategy");
            sb.AppendLine();
            sb.AppendLine("Tracking and accelerating your loan repayments is the highest guaranteed return you can achieve.");
            sb.AppendLine();
            sb.AppendLine("Key Debt Recommendations:");
            sb.AppendLine("1. **Prepayment Savings:** Even adding 1 extra EMI payment per year on a 20-year home loan reduces total tenure by up to 3 to 4 years and saves lakhs in interest.");
            sb.AppendLine("2. **Avalanche Payoff:** Focus any extra cash flow on the loan with the highest interest rate first, while maintaining minimum EMIs on others.");
            sb.AppendLine("3. **Debt-to-Income (DTI) Rule:** Keep total monthly EMI obligations under 35-40% of your take-home income to maintain safe liquidity.");
            sb.AppendLine();
            sb.AppendLine("Check the **Loans & EMI Manager** page to view your full month-by-month amortization schedule and record part-prepayments.");

            followUps.Add("How long will my emergency fund last?");
            followUps.Add("How can I save ₹10,000 more every month?");
        }
        // Default general financial breakdown
        else
        {
            sb.AppendLine($"### 📊 Financial Overview for **{report.ProfileDisplayName}**");
            sb.AppendLine();
            sb.AppendLine($"Your overall financial health is rated **{report.OverallHealthScore}/100 ({report.HealthTier})**.");
            sb.AppendLine($"- **Net Total Balance:** ₹{report.TotalNetBalance:N2}");
            sb.AppendLine($"- **Monthly Inflows:** ₹{report.CurrentMonthIncome:N2}");
            sb.AppendLine($"- **Monthly Outflows:** ₹{report.CurrentMonthExpense:N2}");
            sb.AppendLine($"- **Net Cash Flow:** +₹{report.NetCashFlow:N2} ({report.SavingsRatePct:F1}%)");
            sb.AppendLine($"- **Emergency Runway:** {report.CashRunwayMonths:F1} months");
            sb.AppendLine();
            sb.AppendLine("Ask me any specific question like *'Can I afford a vacation?'*, *'How is my emergency fund?'*, or *'How can I save ₹10,000 more?'*!");

            followUps.Add("Can I afford a ₹25,000 purchase this month?");
            followUps.Add("How long will my emergency fund last?");
            followUps.Add("How can I increase my monthly savings?");
        }

        return new AiAdviceReply(sb.ToString(), followUps, false);
    }

    private static decimal ExtractRupees(string input)
    {
        var words = input.Split(' ', ',', '?', '!', '\t', '₹');
        foreach (var w in words)
        {
            var cleaned = w.Replace("₹", "").Replace(",", "").Trim();
            if (decimal.TryParse(cleaned, out var val) && val > 100)
            {
                return val;
            }
        }
        return 0;
    }

    #endregion
}
