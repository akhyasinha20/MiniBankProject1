﻿
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using MiniBank.Models;
using System.Text.RegularExpressions;
using System.Security.Cryptography;
using System.Text;
using System.Globalization;
using System.Data.Entity;

namespace MiniBank.Controllers
{
    public class EmployeeController : Controller
    {
        // GET: Employee
        private MiniBankDBEntities4 db = new MiniBankDBEntities4();
        public ActionResult Dashboard() => View();

        [HttpGet]
        public ActionResult OpenAccount() => View();

        // added 'email' parameter to receive email from the form
        [HttpPost]
        public ActionResult OpenAccount(string pan, string name, DateTime? dob, decimal? minBalance, string email)
        {
            // Step 1: Check if PAN exists
            if (string.IsNullOrEmpty(name) || !dob.HasValue || !minBalance.HasValue)
            {
                var existing = db.Customers.FirstOrDefault(c => c.PAN == pan);
                if (existing != null)
                {
                    ViewBag.Error = "Account cannot be generated as the PAN card already exists.";
                    return View();
                }

                // If PAN does not exist, prompt for additional details
                ViewBag.PAN = pan;
                ViewBag.Step = 2; // Indicate step 2
                return View();
            }

            // Server-side validations
            var nameRegex = new Regex(@"^[A-Za-z]+$"); // only alphabets, no spaces
            if (string.IsNullOrWhiteSpace(name) || !nameRegex.IsMatch(name.Trim()))
            {
                ViewBag.Error = "Customer name must contain only alphabets (A-Z, a-z) with no spaces.";
                ViewBag.PAN = pan;
                ViewBag.Step = 2;
                ViewBag.Name = name;
                ViewBag.DOB = dob?.ToString("yyyy-MM-dd");
                ViewBag.MinBalance = minBalance;
                ViewBag.Email = email;
                return View();
            }

            if (dob.HasValue && dob.Value.Date > DateTime.Today)
            {
                ViewBag.Error = "Date of Birth cannot be a future date.";
                ViewBag.PAN = pan;
                ViewBag.Step = 2;
                ViewBag.Name = name;
                ViewBag.DOB = dob?.ToString("yyyy-MM-dd");
                ViewBag.MinBalance = minBalance;
                ViewBag.Email = email;
                return View();
            }

            if (minBalance.HasValue && minBalance.Value < 1000)
            {
                ViewBag.Error = "Minimum balance must be ₹1000 or more.";
                ViewBag.PAN = pan;
                ViewBag.Step = 2;
                ViewBag.Name = name;
                ViewBag.DOB = dob?.ToString("yyyy-MM-dd");
                ViewBag.MinBalance = minBalance;
                ViewBag.Email = email;
                return View();
            }

            // Step 2: Create account with additional details
            var cust = new Customer
            {
                CustName = name.Trim(),
                PAN = pan,
                DOB = dob,
                Email = email,
                CreatedAt = DateTime.Now
            };
            db.Customers.Add(cust);
            db.SaveChanges();

            var acc = new Account
            {
                AccountType = "Savings",
                CustomerId = cust.CustomerId,
                CreatedAt = DateTime.Now
            };
            db.Accounts.Add(acc);
            db.SaveChanges();

            var savings = new SavingsAccount
            {
                AccountId = acc.AccountId,
                Balance = minBalance.Value,
                MinBalance = 1000 // Assuming ₹1000 as the minimum balance
            };
            db.SavingsAccounts.Add(savings);
            db.SaveChanges();

            // Generate login credentials for the customer
            var username = cust.CustName;
            var password = "Password1234"; // meets at least one upper, lower, digit

            // ensure username uniqueness
           

            var user = new UserRegister
            {
                Username = username,
                PasswordHash = password, // existing system stores plaintext; keep consistent
                Email = email,
                Role = "Customer",
                ReferenceId = cust.CustomerId,
                IsActive = true,
                CreatedAt = DateTime.Now
            };

            db.UserRegisters.Add(user);
            db.SaveChanges();

            ViewBag.Message = $"Account successfully created! CustomerID: {cust.CustomerId}, AccountID: {acc.AccountId}. Login: {username} / {password}";
            return View();
        }

        // Helper: create a username from PAN (fallback to cust id appended if needed)
        private string GenerateUsernameFromPAN(string pan, int customerId)
        {
            if (string.IsNullOrWhiteSpace(pan)) return "cust" + customerId;
            // remove spaces and make uppercase
            var clean = new string(pan.Where(char.IsLetterOrDigit).ToArray());
            return clean;
        }

        private int GetIntFromByte(byte b) => b;

        [HttpGet]
        public ActionResult DepositWithdraw() => View();

        [HttpPost]
        public ActionResult DepositWithdraw(int accountId, string type, decimal amount)
        {
            var acc = db.SavingsAccounts.Find(accountId);
            if (acc == null)
            {
                ViewBag.Error = "Account not found.";
                return View();
            }

            if (type == "Deposit")
            {
                if (amount < 100)
                {
                    ViewBag.Error = "Deposit must be greater than 100.";
                    return View();
                }
                acc.Balance += amount;
            }
            else if (type == "Withdrawal")
            {
                if (acc.Balance - amount < acc.MinBalance)
                {
                    ViewBag.Error = "Minimum balance ₹1000 required.";
                    return View();
                }
                acc.Balance -= amount;
            }

            db.SavingsTransactions.Add(new SavingsTransaction
            {
                AccountId = acc.AccountId,
                TransactionType = type,
                Amount = amount,
                BalanceAfter = acc.Balance,
                TransactionDate = DateTime.Now

            });
            db.SaveChanges();

            ViewBag.Message = "Transaction successful.";
            return View();
        }

        [HttpGet]
        public ActionResult LoanAccount() => View();

        [HttpPost]
        public ActionResult LoanAccount(int customerId, decimal loanAmount, int tenure)
        {
            var cust = db.Customers.Find(customerId);
            if (cust == null)
            {
                ViewBag.Error = "Customer not found.";
                return View();
            }

            if (!db.Accounts.Any(a => a.CustomerId == customerId && a.AccountType == "Savings"))
            {
                ViewBag.Error = "Customer must have a Savings Account first.";
                return View();
            }

            if (loanAmount < 10000)
            {
                ViewBag.Error = "Minimum loan ₹10,000.";
                return View();
            }

            decimal roi = 10.5M;
            if ((DateTime.Now.Year - (cust.DOB?.Year ?? 0)) >= 60)
            {
                roi = 9.5M;
                if (loanAmount > 100000)
                {
                    ViewBag.Error = "Senior citizen max loan ₹1 Lakh.";
                    return View();
                }
            }

            decimal emi = (loanAmount * roi / 1200) / (1 - (decimal)Math.Pow(1 + (double)(roi / 1200), -tenure));

            var acc = new Account { AccountType = "Loan", CustomerId = customerId };
            db.Accounts.Add(acc);
            db.SaveChanges();

            db.LoanAccounts.Add(new LoanAccount
            {
                AccountId = acc.AccountId,
                LoanAmount = loanAmount,
                StartDate = DateTime.Now,
                TenureMonths = tenure,
                AnnualROI = roi,
                EMI = emi,
                OutstandingAmount = loanAmount
            });
            db.SaveChanges();

            ViewBag.Message = $"Loan Account created. ID: {acc.AccountId}, EMI: ₹{emi:F2}";
            return View();
        }

        [HttpGet]
        public ActionResult Report()
        {
            ViewBag.Step = 1;
            return View();
        }

        // Accept posted date strings in yyyy/MM/dd and parse them explicitly.
        [HttpPost]
        public ActionResult Report(string type, string fromDate, string toDate)
        {
            // Validate presence
            if (string.IsNullOrWhiteSpace(fromDate) || string.IsNullOrWhiteSpace(toDate))
            {
                ViewBag.Error = "Please select a valid date range (format: yyyy/MM/dd).";
                return View();
            }

            // Parse using exact format yyyy/MM/dd
            if (!DateTime.TryParseExact(fromDate, "yyyy/MM/dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime parsedFrom)
                || !DateTime.TryParseExact(toDate, "yyyy/MM/dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime parsedTo))
            {
                ViewBag.Error = "Dates must be in format yyyy/MM/dd.";
                return View();
            }

            if (parsedFrom > parsedTo)
            {
                ViewBag.Error = "From Date cannot be later than To Date.";
                return View();
            }

            try
            {
                var from = parsedFrom.Date;
                var to = parsedTo.Date;

                if (type == "Savings")
                {
                    var savingsTransactions = db.SavingsTransactions
                        .Where(t => DbFunctions.TruncateTime(t.TransactionDate) >= from && DbFunctions.TruncateTime(t.TransactionDate) <= to)
                        .OrderByDescending(t => t.TransactionDate)
                        .Select(t => new
                        {
                            t.TransactionId,
                            t.AccountId,
                            t.TransactionType,
                            t.Amount,
                            BalanceAfter = t.BalanceAfter,
                            TransactionDate = t.TransactionDate
                        })
                        .ToList();
                    ViewBag.Transactions = savingsTransactions;
                    ViewBag.TransactionType = "Savings";
                }
                else if (type == "Loan")
                {
                    var loanTransactions = db.LoanTransactions
                        .Where(t => DbFunctions.TruncateTime(t.TransDate) >= from && DbFunctions.TruncateTime(t.TransDate) <= to)
                        .OrderByDescending(t => t.TransDate)
                        .Select(t => new
                        {
                            t.TransactionId,
                            AccountId = t.LoanAccountId,
                            TransactionType = "EMI Payment",
                            t.Amount,
                            BalanceAfter = t.OutstandingAfter,
                            TransactionDate = t.TransDate
                        })
                        .ToList();
                    ViewBag.Transactions = loanTransactions;
                    ViewBag.TransactionType = "Loan";
                }
                else
                {
                    ViewBag.Error = "Invalid transaction type selected.";
                    return View();
                }

                            // Keep display date in yyyy/MM/dd (matches user requirement)
                ViewBag.FromDate = from.ToString("yyyy/MM/dd");
                ViewBag.ToDate = to.ToString("yyyy/MM/dd");

                // For compatibility if you still want an input-friendly format (not required now)
                ViewBag.FromDateInput = ViewBag.FromDate;
                ViewBag.ToDateInput = ViewBag.ToDate;

                ViewBag.SelectedType = type;
                return View();
            }
            catch (Exception ex)
            {
                ViewBag.Error = "Error fetching transactions: " + ex.Message;
                return View();
            }
        }
       
        }

        }
    
