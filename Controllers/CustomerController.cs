using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using MiniBank.Models;

namespace MiniBank.Controllers
{
    public class CustomerController : Controller
    {
        // GET: Customer
        private MiniBankDBEntities4 db = new MiniBankDBEntities4();
        public ActionResult Dashboard()
        {
            var uname = Session["Username"];
            // Get customer details
            var customer = db.Customers.FirstOrDefault(c => c.CustName == uname);

            if (customer == null)
            {

                return RedirectToAction("Login", "Auth");
            }
            var customerId = customer.CustomerId;

            // Fetch savings account
            var account = db.Accounts.FirstOrDefault(a => a.CustomerId == customerId && a.AccountType == "Savings");

            // Prepare ViewBag data
            ViewBag.Customer = customer;
            ViewBag.Account = account;

            return View();
        }

        //// GET: DepositWithdraw
        //public ActionResult DepositWithdraw()
        //{
        //    return View();
        //}

        //// POST: DepositWithdraw
        //[HttpPost]
        //public ActionResult DepositWithdraw(int AccountId, string transactionType, decimal amount)
        //{
        //    ViewBag.Message = null;



        //    var account = db.SavingsAccounts.FirstOrDefault(a => a.AccountId == AccountId);

        //    if (account == null)
        //    {
        //        ViewBag.Message = "❌ Account ID not found.";
        //        return View();
        //    }

        //    if (transactionType == "Deposit")
        //    {
        //        if (amount < 100)
        //        {
        //            ViewBag.Message = "⚠️ Minimum deposit should be ₹100.";
        //            return View();
        //        }

        //        account.Balance += amount;

        //        // Add transaction entry with BalanceAfter
        //        var txn = new SavingsTransaction
        //        {
        //            AccountId = AccountId,
        //            TransactionType = "Deposit",
        //            Amount = amount,
        //            BalanceAfter = account.Balance,
        //            TransactionDate = DateTime.Now
        //        };
        //        db.SavingsTransactions.Add(txn);
        //        db.SaveChanges();

        //        ViewBag.Message = $"✅ ₹{amount} deposited successfully!";
        //    }
        //    else if (transactionType == "Withdraw")
        //    {
        //        if (amount <= 0)
        //        {
        //            ViewBag.Message = "⚠️ Invalid withdrawal amount.";
        //            return View();
        //        }

        //        if (account.Balance - amount < 1000)
        //        {
        //            ViewBag.Message = "❌ Insufficient balance. Minimum balance of ₹1000 must be maintained.";
        //            return View();
        //        }

        //        account.Balance -= amount;

        //        var txn = new SavingsTransaction
        //        {
        //            AccountId = AccountId,
        //            TransactionType = "Withdrawal",
        //            Amount = amount,
        //            BalanceAfter = account.Balance,
        //            TransactionDate = DateTime.Now
        //        };
        //        db.SavingsTransactions.Add(txn);
        //        db.SaveChanges();

        //        ViewBag.Message = $"✅ ₹{amount} withdrawn successfully!";
        //    }
        //    else
        //    {
        //        ViewBag.Message = "⚠️ Invalid transaction type.";
        //    }

        //    return View();
        //}



        public ActionResult ViewTransactions(string type = "Savings")
        {
            var uname = Session["Username"];
            // Get customer details
            var customer = db.Customers.FirstOrDefault(c => c.CustName == uname);
            if (customer == null) return RedirectToAction("Login", "Auth");
            var customerId = customer.CustomerId;

            ViewBag.SelectedType = type ?? "Savings";

            if (string.Equals(ViewBag.SelectedType, "Loan", StringComparison.OrdinalIgnoreCase))
            {
                // get loan account ids for this customer
                var loanAccountIds = db.Accounts
                    .Where(a => a.CustomerId == customerId && a.AccountType == "Loan")
                    .Select(a => a.AccountId)
                    .ToList();

                var loanTx = db.LoanTransactions
                    .Where(t => loanAccountIds.Contains(t.LoanAccountId))
                    .OrderByDescending(t => t.TransDate)
                    .ToList();

                ViewBag.Transactions = loanTx;
            }
            else
            {
                // savings transactions
                var savingsAccountIds = db.Accounts
                    .Where(a => a.CustomerId == customerId && a.AccountType == "Savings")
                    .Select(a => a.AccountId)
                    .ToList();

                var savingsTx = db.SavingsTransactions
                    .Where(t => savingsAccountIds.Contains(t.AccountId))
                    .OrderByDescending(t => t.TransactionDate)
                    .ToList();

                ViewBag.Transactions = savingsTx;
            }

            return View();
        }

        [HttpGet]
        public ActionResult Transfer()
        {
            return View();
        }

        [HttpPost]
        public ActionResult Transfer(int fromId, int toId, decimal amount)
        {
            ViewBag.Error = null;
            ViewBag.Message = null;

            // Input validation
            if (amount <= 0)
            {
                ViewBag.Error = "⚠️ Please enter a valid transfer amount.";
                return View();
            }

            if (fromId == toId)
            {
                ViewBag.Error = "⚠️ Source and destination accounts must be different.";
                return View();
            }

            // Load accounts
            var from = db.SavingsAccounts.Find(fromId);
            var to = db.SavingsAccounts.Find(toId);

            if (from == null || to == null)
            {
                ViewBag.Error = "❌ One or both accounts not found.";
                return View();
            }

            // Sufficient funds check with MinBalance constraint
            if (from.Balance - amount < from.MinBalance)
            {
                ViewBag.Error = "❌ Insufficient balance. Minimum balance must be maintained.";
                return View();
            }

            // Perform atomic transfer inside a DB transaction
            using (var tx = db.Database.BeginTransaction())
            {
                try
                {
                    // Deduct from source
                    from.Balance -= amount;

                    var debitTxn = new SavingsTransaction
                    {
                        AccountId = fromId,
                        TransactionType = "Transfer",
                        Amount = amount,
                        BalanceAfter = from.Balance,
                        TransactionDate = DateTime.Now
                    };
                    db.SavingsTransactions.Add(debitTxn);

                    // Credit to destination
                    to.Balance += amount;

                    var creditTxn = new SavingsTransaction
                    {
                        AccountId = toId,
                        TransactionType = "Transfer",
                        Amount = amount,
                        BalanceAfter = to.Balance,
                        TransactionDate = DateTime.Now
                    };
                    db.SavingsTransactions.Add(creditTxn);

                    db.SaveChanges();
                    tx.Commit();

                    ViewBag.Message = $"✅ Transfer of ₹{amount} from Account #{fromId} to Account #{toId} completed successfully.";
                }
                catch (Exception ex)
                {
                    try { tx.Rollback(); } catch { /* ignore rollback errors */ }
                    // Log exception if logging exists (not added here to keep minimal)
                    ViewBag.Error = "❌ Transfer failed. Please try again later.";
                }
            }

            return View();
        }

        [HttpGet]
        public ActionResult PayEMI() => View();

        [HttpPost]
        public ActionResult PayEMI(int loanId, decimal amount)
        {
            var loan = db.LoanAccounts.Find(loanId);
            if (loan == null)
            {
                ViewBag.Error = "Loan not found.";
                return View();
            }

            loan.OutstandingAmount -= amount;
            if (loan.OutstandingAmount <= 0) loan.IsClosed = true;

            db.LoanTransactions.Add(new LoanTransaction
            {
                LoanAccountId = loanId,
                Amount = amount,
                OutstandingAfter = (decimal)loan.OutstandingAmount,
                TransDate = DateTime.Now
            });
            db.SaveChanges();

            ViewBag.Message = "EMI paid successfully.";
            return View();
        }
        // RESET PASSWORD
        [HttpGet]
        public ActionResult ResetPassword()
        {
            return View();
        }

        [HttpPost]
        public ActionResult ResetPassword(string Username, string oldPassword, string newPassword)
        {
            var uname = Session["Username"];
            if (string.IsNullOrEmpty(oldPassword) || string.IsNullOrEmpty(newPassword))
            {
                ViewBag.Message = "Please fill all fields.";
                return View();
            }

            var user = db.UserRegisters.FirstOrDefault(u => u.Username ==(string) uname && u.PasswordHash == oldPassword);

            //if (user == null)
            //{
            //    ViewBag.Message = "Invalid Customer ID or Password.";
            //    return View();
            //}

            user.PasswordHash = newPassword;
            db.SaveChanges();

            ViewBag.Message = "✅ Password reset successful!";
            return View();
        }
    }

}