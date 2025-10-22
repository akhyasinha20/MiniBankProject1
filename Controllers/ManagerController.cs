using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using MiniBank.Models;

namespace MiniBank.Controllers
{   
    public class ManagerController : Controller
    {   private MiniBankDBEntities4 db = new MiniBankDBEntities4();
        // GET: Manager
        public ActionResult Dashboard()
        {
            ViewBag.TotalEmployees = db.UserRegisters.Count(u => u.Role == "Employee");
            ViewBag.TotalCustomers = db.Customers.Count();
            return View();
        }

        public ActionResult AddEmployee()
        {
            var emps = db.UserRegisters.Where(u => u.Role == "Employee").ToList();
            return View(emps);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Approve(int id)
        {
            var usr = db.UserRegisters.Find(id);
            if (usr == null)
            {
                TempData["Error"] = "User not found.";
                return RedirectToAction("AddEmployee");
            }

            using (var transaction = db.Database.BeginTransaction())
            {
                try
                {
                    if (!usr.ReferenceId.HasValue)
                    {
                        var employee = new Employee
                        {
                            EmployeeName = usr.Username,
                            DeptId = "DEPT01",
                            Email = usr.Email,
                            IsActive = true,
                            CreatedAt = DateTime.Now
                        };

                        db.Employees.Add(employee);
                        db.SaveChanges(); // obtain EmpId

                        usr.ReferenceId = employee.EmpId;
                    }
                    else
                    {
                        var existingEmp = db.Employees.Find(usr.ReferenceId.Value);
                        if (existingEmp != null)
                        {
                            existingEmp.IsActive = true;
                        }
                    }
                    usr.IsActive = true;
                    db.SaveChanges();

                    transaction.Commit();

                    TempData["Success"] = "Employee approved and added successfully.";
                }
                catch (Exception ex)
                {
                    transaction.Rollback();
                    TempData["Error"] = "Failed to approve employee: " + ex.Message;
                    // Optional: log the exception
                }
            }

            return RedirectToAction("AddEmployee");
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Remove(int id)
        {
            using (var transaction = db.Database.BeginTransaction())
            {
                try
                {
                    var usr = db.UserRegisters.Find(id);
                    if (usr != null)
                    {
                        if (usr.ReferenceId.HasValue)
                        {
                            var emp = db.Employees.Find(usr.ReferenceId.Value);
                            if (emp != null)
                            {
                                db.Employees.Remove(emp);
                            }
                        }

                        db.UserRegisters.Remove(usr);
                        db.SaveChanges();
                    }

                    transaction.Commit();

                    TempData["Success"] = "Employee removed.";
                }
                catch (Exception ex)
                {
                    transaction.Rollback();
                    TempData["Error"] = "Failed to remove employee: " + ex.Message;
                    // Optional: log the exception
                }
            }

            return RedirectToAction("AddEmployee");
        }

        public ActionResult ViewTransactions()
        {
            var tx = db.SavingsTransactions.OrderByDescending(t => t.TransactionDate).ToList();
            return View(tx);
        }

        public ActionResult ManageCustomers()
        {
            return View(db.Customers.ToList());
        }

        public ActionResult DeactivateCustomer(int id)
        {
            var cust = db.Customers.Find(id);
            if (cust != null)
            {
                var usr = db.UserRegisters.FirstOrDefault(u => u.ReferenceId == cust.CustomerId);
                if (usr != null) usr.IsActive = false;
                db.SaveChanges();
            }
            return RedirectToAction("ManageCustomers");
        }
    }
}