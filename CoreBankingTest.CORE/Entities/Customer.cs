using CoreBankingTest.CORE.Interfaces;
using CoreBankingTest.CORE.Models;
using CoreBankingTest.CORE.ValueObjects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CoreBankingTest.CORE.Entities

{
    public class Customer : ISoftDelete
    {
        public CustomerId CustomerId { get; private set; }
        public string FirstName { get; private set; }
        public string LastName { get; private set; }
        public string Email { get; private set; }
        public string PhoneNumber { get; private set; }
        public DateTime DateCreated { get; private set; }
        public bool IsActive { get; private set; }
        public bool IsDeleted { get; private set; }
        public DateTime? DeletedAt { get; private set; }
        public string? DeletedBy { get; private set; }
        public string Address { get; private set; }
        public string BVN { get; private set; }
        public int CreditScore { get; private set; }
        public DateTime DateOfBirth { get; private set; }
       

        // Navigation property for accounts
        private readonly List<Account> _accounts = new();
        public IReadOnlyCollection<Account> Accounts => _accounts.AsReadOnly();

        public Customer() { } // EF Core needs this

        public Customer(string firstName, string lastName, string email, string phoneNumber, DateTime dateOfBirth
            ,string bVN, int creditScore)
        {
            CustomerId = CustomerId.Create();
            FirstName = firstName ?? throw new ArgumentNullException(nameof(firstName));
            LastName = lastName ?? throw new ArgumentNullException(nameof(lastName));
            Email = email ?? throw new ArgumentNullException(nameof(email));
            PhoneNumber = phoneNumber ?? throw new ArgumentNullException(nameof(phoneNumber));
            DateCreated = DateTime.UtcNow;
            BVN = bVN;
            CreditScore = creditScore;
            IsActive = true;
            DateOfBirth = dateOfBirth;
        }

       
        
        public void UpdateContactInfo(string email, string phoneNumber)
        {
            if (!IsActive)
                throw new InvalidOperationException("Cannot update inactive customer");

            Email = email;
            PhoneNumber = phoneNumber;
        }

        //public static Customer Create(string firstName, string lastName, string email, string phoneNumber, string address, DateTime dateOfBirth, string bvn, int creditScore)
        //{
        //    return new Customer(
        //        firstName: firstName,
        //        lastName: lastName,
        //        email: email,
        //        phoneNumber: phoneNumber,
        //        address: address,
        //        dateOfBirth: dateOfBirth,
        //        bVN: bvn,
        //        creditScore: creditScore
                
        //    );
        //}

        public void Deactivate()
        {
            if (_accounts.Any(a => a.Balance.Amount > 0))
                throw new InvalidOperationException("Cannot deactivate customer with account balance");

            IsActive = false;
        }

        internal void AddAccount(Account account)
        {
            _accounts.Add(account);
        }

        public void SoftDelete(string deletedBy)
        {
            if (Accounts.Any(a => a.Balance.Amount > 0))
                throw new InvalidOperationException("Cannot delete customer with account balance");

            IsDeleted = true;
            DeletedAt = DateTime.UtcNow;
            DeletedBy = deletedBy;
        }
    }
}