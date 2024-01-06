using Expense_Tracker.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

namespace Expense_Tracker.Controllers
{
    public class DashboardController : Controller
    {
        private readonly ApplicationDbContext _context;
        private DateTime _startDate;
        private DateTime _endDate;

        public DashboardController(ApplicationDbContext context)
        {
            _context = context;
            _startDate = DateTime.Today.AddDays(-6);
            _endDate = DateTime.Today;
        }

        public async Task<ActionResult> FilterDates(DateTime? date_start, DateTime? date_end)
        {
            if (date_start.HasValue && date_end.HasValue)
            {
                if (date_start.Value > date_end.Value)
                {                    
                    ModelState.AddModelError("", "Start date cannot be greater than end date.");
                    return View("Index");
                }
                else if (!date_start.HasValue || !date_end.HasValue)
                {
                    ModelState.AddModelError("", "Both start date and end date are required.");
                    return View("Index");
                }

                else if (date_start.Value > DateTime.Now || date_end.Value > DateTime.Now)
                {
                    ModelState.AddModelError("", "Dates cannot be in the future.");
                    return View("Index");
                }
                else {
                    _startDate = date_start.Value;
                    _endDate = date_end.Value;

                    ViewBag.StartDate = _startDate;
                    ViewBag.EndDate = _endDate;

                    TempData["StartDate"] = _startDate;
                    TempData["EndDate"] = _endDate;
                }                
            }
            else
            {
                ViewBag.StartDate = _startDate;
                ViewBag.EndDate = _endDate;
            }

            return RedirectToAction("Index");
        }

        public async Task<ActionResult> Index()
        {
            ViewBag.StartDate = TempData["StartDate"] != null ? (DateTime)TempData["StartDate"] : _startDate;
            ViewBag.EndDate = TempData["EndDate"] != null ? (DateTime)TempData["EndDate"] : _endDate;

            //Last 7 Days
            DateTime StartDate = ViewBag.StartDate;
            DateTime EndDate = ViewBag.EndDate;

            List<Transaction> SelectedTransactions = await _context.Transactions
                .Include(x => x.Category)
                .Where(y => y.Date >= StartDate && y.Date <= EndDate)
                .ToListAsync();

            //Total Income
            int TotalIncome = SelectedTransactions
                .Where(i => i.Category.Type == "Income")
                .Sum(j => j.Amount);
            ViewBag.TotalIncome = TotalIncome.ToString("C0");

            //Total Expense
            int TotalExpense = SelectedTransactions
                .Where(i => i.Category.Type == "Expense")
                .Sum(j => j.Amount);
            ViewBag.TotalExpense = TotalExpense.ToString("C0");

            //Balance
            int Balance = TotalIncome - TotalExpense;
            CultureInfo culture = CultureInfo.CreateSpecificCulture("en-US");
            culture.NumberFormat.CurrencyNegativePattern = 1;
            ViewBag.Balance = String.Format(culture, "{0:C0}", Balance);

            //Doughnut Chart - Expense By Category
            ViewBag.DoughnutChartData = SelectedTransactions
                .Where(i => i.Category.Type == "Expense")
                .GroupBy(j => j.Category.CategoryId)
                .Select(k => new
                {
                    categoryTitleWithIcon = k.First().Category.Icon + " " + k.First().Category.Title,
                    amount = k.Sum(j => j.Amount),
                    formattedAmount = k.Sum(j => j.Amount).ToString("C0"),
                })
                .OrderByDescending(l => l.amount)
                .ToList();

            //Spline Chart - Income vs Expense

            //Income
            List<SplineChartData> IncomeSummary = SelectedTransactions
                .Where(i => i.Category.Type == "Income")
                .GroupBy(j => j.Date)
                .Select(k => new SplineChartData()
                {
                    day = k.First().Date.ToString("dd-MMM"),
                    income = k.Sum(l => l.Amount)
                })
                .ToList();

            //Expense
            List<SplineChartData> ExpenseSummary = SelectedTransactions
                .Where(i => i.Category.Type == "Expense")
                .GroupBy(j => j.Date)
                .Select(k => new SplineChartData()
                {
                    day = k.First().Date.ToString("dd-MMM"),
                    expense = k.Sum(l => l.Amount)
                })
                .ToList();

            //Combine Income & Expense
            int numberOfDays = (EndDate - StartDate).Days + 1;

            List<string> dateList = new List<string>();
            DateTime currentDate = StartDate; // Assuming StartDate is defined somewhere

            for (int i = 0; i < numberOfDays; i++)
            {
                dateList.Add(currentDate.ToString("dd-MMM"));
                currentDate = currentDate.AddDays(1);
            }

            string[] dateArray = dateList.ToArray();

            ViewBag.SplineChartData = from day in dateArray
                                      join income in IncomeSummary on day equals income.day into dayIncomeJoined
                                      from income in dayIncomeJoined.DefaultIfEmpty()
                                      join expense in ExpenseSummary on day equals expense.day into expenseJoined
                                      from expense in expenseJoined.DefaultIfEmpty()
                                      select new
                                      {
                                          day = day,
                                          income = income == null ? 0 : income.income,
                                          expense = expense == null ? 0 : expense.expense,
                                      };

            //Recent Transactions
            ViewBag.RecentTransactions = await _context.Transactions
                .Include(i => i.Category)
                .OrderByDescending(j => j.Date)
                .Take(5)
                .ToListAsync();

            return View();
        }
    }

    public class SplineChartData
    {
        public string day;
        public int income;
        public int expense;

    }
}