
namespace LibraryManagement
{
    // Enums
    public enum ItemStatus { Available, Borrowed, Reserved, Damaged }
    public enum ItemCondition { New, Good, Worn, Damaged }

    // Custom Exceptions
    public class CannotBorrowException : Exception
    {
        public CannotBorrowException(string message) : base(message) { }
    }

    public class MaximumItemsReachedException : Exception
    {
        public MaximumItemsReachedException(string message) : base(message) { }
    }

    // Interface for borrowable items
    public interface ILoanable
    {
        string Id { get; }
        string Title { get; }
        int PublicationYear { get; }
        ItemStatus Status { get; }
        int MaxBorrowDays { get; }
        ItemCondition Condition { get; }
    }

    // Abstract base for library items
    public abstract class LibraryItem : ILoanable
    {
        private readonly string _id;
        private string _title;
        private int _publicationYear;
        private ItemStatus _status;
        private ItemCondition _condition;
        private readonly Queue<string> _reservationQueue = new();

        protected LibraryItem(string id, string title, int publicationYear, ItemCondition condition = ItemCondition.Good)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Id cannot be empty.");
            _id = id;
            Title = title;
            PublicationYear = publicationYear; 
            _condition = condition;
            _status = ItemStatus.Available;
        }

        public string Id => _id;

        public string Title
        {
            get => _title;
            set
            {
                if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("Title cannot be empty.");
                _title = value.Trim();
            }
        }

        public int PublicationYear
        {
            get => _publicationYear;
            set
            {
                var currentYear = DateTime.Now.Year;
                if (value > currentYear) throw new ArgumentException("Publication year cannot be in the future.");
                _publicationYear = value;
            }
        }

        public ItemStatus Status
        {
            get => _status;
            internal set => _status = value;
        }

        public ItemCondition Condition
        {
            get => _condition;
            private set => _condition = value;
        }

        public void ReportDamage(ItemCondition condition)
        {
            if (condition == ItemCondition.New) throw new ArgumentException("Invalid damage report.");
            Condition = condition;
            if (condition == ItemCondition.Damaged)
                Status = ItemStatus.Damaged;
        }

        // Reservation queue operations (composition)
        public void EnqueueReservation(LibraryMember member)
        {
            if (member == null) throw new ArgumentNullException(nameof(member));
            if (!_reservationQueue.Contains(member.Id))
                _reservationQueue.Enqueue(member.Id);
        }

        public void DequeueReservation()
        {
            if (_reservationQueue.Count > 0) _reservationQueue.Dequeue();
        }

        public string PeekReservation() => _reservationQueue.Count > 0 ? _reservationQueue.Peek() : null;

        public bool HasReservations => _reservationQueue.Count > 0;

        public abstract int MaxBorrowDays { get; }

        public override string ToString()
        {
            return $"{GetType().Name} [{Id}] \"{Title}\" ({PublicationYear}) - Status: {Status}, Condition: {Condition}";
        }
    }

    // Derived item types
    public class Book : LibraryItem
    {
        private string _author;
        public Book(string id, string title, string author, int publicationYear, ItemCondition condition = ItemCondition.Good)
            : base(id, title, publicationYear, condition)
        {
            Author = author;
        }

        public string Author
        {
            get => _author;
            set
            {
                if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("Author cannot be empty.");
                _author = value.Trim();
            }
        }

        public override int MaxBorrowDays => 21;

        public override string ToString()
        {
            return base.ToString() + $", Author: {Author}, MaxDays: {MaxBorrowDays}";
        }
    }

    public class Magazine : LibraryItem
    {
        private int _issueNumber;
        private int _publicationMonth;
        public Magazine(string id, string title, int issueNumber, int publicationMonth, int publicationYear, ItemCondition condition = ItemCondition.Good)
            : base(id, title, publicationYear, condition)
        {
            IssueNumber = issueNumber;
            PublicationMonth = publicationMonth;
        }

        public int IssueNumber
        {
            get => _issueNumber;
            set
            {
                if (value <= 0) throw new ArgumentException("Issue number must be positive.");
                _issueNumber = value;
            }
        }

        public int PublicationMonth
        {
            get => _publicationMonth;
            set
            {
                if (value < 1 || value > 12) throw new ArgumentException("Publication month must be between 1 and 12.");
                _publicationMonth = value;
            }
        }

        public override int MaxBorrowDays => 7;

        public override string ToString()
        {
            return base.ToString() + $", Issue: {IssueNumber}, Month: {PublicationMonth}, MaxDays: {MaxBorrowDays}";
        }
    }

    public class Audiobook : LibraryItem
    {
        private string _narrator;
        private int _durationMinutes;
        public Audiobook(string id, string title, string narrator, int durationMinutes, int publicationYear, ItemCondition condition = ItemCondition.Good)
            : base(id, title, publicationYear, condition)
        {
            Narrator = narrator;
            DurationMinutes = durationMinutes;
        }

        public string Narrator
        {
            get => _narrator;
            set
            {
                if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("Narrator cannot be empty.");
                _narrator = value.Trim();
            }
        }

        public int DurationMinutes
        {
            get => _durationMinutes;
            set
            {
                if (value <= 0) throw new ArgumentException("Duration must be positive.");
                _durationMinutes = value;
            }
        }

        public override int MaxBorrowDays => 14;

        public override string ToString()
        {
            return base.ToString() + $", Narrator: {Narrator}, Duration: {DurationMinutes}min, MaxDays: {MaxBorrowDays}";
        }
    }

    // Members
    public abstract class LibraryMember
    {
        private readonly string _id;
        private string _name;
        protected readonly List<Loan> _loans = new();

        protected LibraryMember(string id, string name)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Id cannot be empty.");
            _id = id;
            Name = name;
        }

        public string Id => _id;
        public string Name
        {
            get => _name;
            set
            {
                if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("Name cannot be empty.");
                _name = value.Trim();
            }
        }

        public IEnumerable<Loan> Loans => _loans.AsReadOnly();

        public abstract int MaxItems { get; }

        public abstract int LoanPeriodDays { get; }

        public abstract bool CanReserve { get; }

        public int CurrentBorrowedCount => _loans.Count(l => !l.IsReturned);

        public bool HasOverdueLoans(DateOnly asOf)
        {
            return _loans.Any(l => !l.IsReturned && l.DueDate < asOf);
        }

        public void AddLoan(Loan loan)
        {
            if (loan == null) throw new ArgumentNullException(nameof(loan));
            _loans.Add(loan);
        }

        public override string ToString()
        {
            return $"{GetType().Name} [{Id}] {Name} - Borrowed: {CurrentBorrowedCount}/{MaxItems}";
        }
    }

    public class RegularMember : LibraryMember
    {
        public RegularMember(string id, string name) : base(id, name) { }
        public override int MaxItems => 5;
        public override int LoanPeriodDays => 21;
        public override bool CanReserve => false;
    }

    public class PremiumMember : LibraryMember
    {
        public PremiumMember(string id, string name) : base(id, name) { }
        public override int MaxItems => 12;
        public override int LoanPeriodDays => 35;
        public override bool CanReserve => true;
    }

    // Loan class
    public class Loan
    {
        private static readonly decimal FinePerDay = 1.5m;

        public Loan(LibraryMember member, LibraryItem item, DateOnly borrowDate, DateOnly dueDate)
        {
            Member = member ?? throw new ArgumentNullException(nameof(member));
            Item = item ?? throw new ArgumentNullException(nameof(item));
            BorrowDate = borrowDate;
            DueDate = dueDate;
            ReturnDate = null;
            Renewed = false;
        }

        public LibraryMember Member { get; }
        public LibraryItem Item { get; }
        public DateOnly BorrowDate { get; }
        public DateOnly DueDate { get; private set; }
        public DateOnly? ReturnDate { get; private set; }
        public bool Renewed { get; private set; }

        public bool IsReturned => ReturnDate.HasValue;

        public decimal CalculateFine(DateOnly asOf)
        {
            DateOnly effectiveReturn = ReturnDate ?? asOf;
            if (effectiveReturn <= DueDate) return 0m;
            var overdueDays = (effectiveReturn.ToDateTime(TimeOnly.MinValue) - DueDate.ToDateTime(TimeOnly.MinValue)).Days;
            return overdueDays * FinePerDay;
        }

        public void Return(DateOnly returnDate)
        {
            if (ReturnDate.HasValue) throw new InvalidOperationException("Already returned.");
            if (returnDate < BorrowDate) throw new ArgumentException("Return date cannot be before borrow date.");
            ReturnDate = returnDate;
            Item.Status = Item.HasReservations ? ItemStatus.Reserved : ItemStatus.Available;
        }

        public void Renew(int extraDays)
        {
            if (IsReturned) throw new InvalidOperationException("Cannot renew a returned loan.");
            if (Renewed) throw new InvalidOperationException("Loan already renewed once.");
            if (extraDays <= 0) throw new ArgumentException("Extra days must be positive.");
            DueDate = DueDate.AddDays(extraDays);
            Renewed = true;
        }

        public override string ToString()
        {
            return $"Loan: Member={Member.Name} [{Member.Id}] Item={Item.Title} [{Item.Id}] Borrowed={BorrowDate} Due={DueDate} Returned={(ReturnDate?.ToString() ?? "Not yet")}";
        }
    }

    // Overdue DTO
    public record OverdueLoanInfo(string MemberId, string MemberName, string ItemId, string ItemTitle, DateOnly DueDate, decimal Fine);

    // Main Library coordinator
    public class Library
    {
        private readonly Dictionary<string, LibraryItem> _items = new();
        private readonly Dictionary<string, LibraryMember> _members = new();
        private readonly List<Loan> _loans = new();

        public void AddItem(LibraryItem item)
        {
            if (item == null) throw new ArgumentNullException(nameof(item));
            if (_items.ContainsKey(item.Id)) throw new ArgumentException($"Item with id {item.Id} already exists.");
            _items[item.Id] = item;
        }

        public void RegisterMember(LibraryMember member)
        {
            if (member == null) throw new ArgumentNullException(nameof(member));
            if (_members.ContainsKey(member.Id)) throw new ArgumentException($"Member with id {member.Id} already exists.");
            _members[member.Id] = member;
        }

        public LibraryItem GetItem(string itemId) => _items.TryGetValue(itemId, out var it) ? it : throw new KeyNotFoundException("Item not found.");
        public LibraryMember GetMember(string memberId) => _members.TryGetValue(memberId, out var m) ? m : throw new KeyNotFoundException("Member not found.");

        public Loan BorrowItem(string memberId, string itemId, DateOnly borrowDate)
        {
            var member = GetMember(memberId);
            var item = GetItem(itemId);

            if (item.Status == ItemStatus.Damaged) throw new CannotBorrowException("Item is damaged and cannot be borrowed.");
            if (member.CurrentBorrowedCount >= member.MaxItems) throw new MaximumItemsReachedException($"Member has reached maximum allowed items ({member.MaxItems}).");
            if (member.HasOverdueLoans(borrowDate)) throw new CannotBorrowException("Member has overdue loans and cannot borrow new items.");
            if (item.Status == ItemStatus.Borrowed) throw new CannotBorrowException("Item is already borrowed.");
            if (item.Status == ItemStatus.Reserved)
            {
                var next = item.PeekReservation();
                if (next != member.Id) throw new CannotBorrowException("Item is reserved by another member.");
                // if same member is at head, allow borrow and remove reservation
                item.DequeueReservation();
            }

            // Determine allowed days: member loan period or item max days - choose the smaller of the two to respect item limits
            int allowedDays = Math.Min(member.LoanPeriodDays, item.MaxBorrowDays);
            var due = borrowDate.AddDays(allowedDays);

            var loan = new Loan(member, item, borrowDate, due);
            _loans.Add(loan);
            member.AddLoan(loan);
            item.Status = ItemStatus.Borrowed;
            return loan;
        }

        public decimal ReturnItem(string memberId, string itemId, DateOnly returnDate)
        {
            var member = GetMember(memberId);
            var loan = member.Loans.FirstOrDefault(l => l.Item.Id == itemId && !l.IsReturned);
            if (loan == null) throw new InvalidOperationException("Active loan not found for this member and item.");
            loan.Return(returnDate);

            // If reserved by someone else, keep status Reserved and do not assign automatically; in a real system you'd notify
            if (loan.Item.HasReservations)
            {
                loan.Item.Status = ItemStatus.Reserved;
            }
            else
            {
                loan.Item.Status = ItemStatus.Available;
            }

            var fine = loan.CalculateFine(returnDate);
            return fine;
        }

        public void RenewLoan(string memberId, string itemId)
        {
            var member = GetMember(memberId);
            var loan = member.Loans.FirstOrDefault(l => l.Item.Id == itemId && !l.IsReturned);
            if (loan == null) throw new InvalidOperationException("Active loan not found to renew.");
            // Disallow renew if there are reservations
            if (loan.Item.HasReservations) throw new CannotBorrowException("Cannot renew; item has reservations.");
            // Extend by member's loan period but ensure not to exceed item's max borrow days since original borrow.
            // For simplicity, allow adding member.LoanPeriodDays but business rule: renew only once.
            loan.Renew(member.LoanPeriodDays);
        }

        public IEnumerable<OverdueLoanInfo> ListOverdueLoans(DateOnly asOf)
        {
            return _loans
                .Where(l => !l.IsReturned && l.DueDate < asOf)
                .Select(l => new OverdueLoanInfo(l.Member.Id, l.Member.Name, l.Item.Id, l.Item.Title, l.DueDate, l.CalculateFine(asOf)))
                .ToList();
        }

        public IEnumerable<LibraryItem> FindItemsByTitle(string partial)
        {
            if (string.IsNullOrWhiteSpace(partial)) return Enumerable.Empty<LibraryItem>();
            partial = partial.Trim().ToLowerInvariant();
            return _items.Values.Where(i => i.Title.ToLowerInvariant().Contains(partial));
        }

        public IEnumerable<LibraryItem> SearchByAuthorOrNarrator(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return Enumerable.Empty<LibraryItem>();
            name = name.Trim().ToLowerInvariant();
            return _items.Values.Where(i =>
                (i is Book b && b.Author.ToLowerInvariant().Contains(name)) ||
                (i is Audiobook a && a.Narrator.ToLowerInvariant().Contains(name))
            );
        }

        public void ReserveItem(string memberId, string itemId)
        {
            var member = GetMember(memberId);
            var item = GetItem(itemId);

            if (!member.CanReserve) throw new CannotBorrowException("Member is not allowed to make reservations.");
            if (item.Condition == ItemCondition.Damaged) throw new CannotBorrowException("Cannot reserve a damaged item.");
            item.EnqueueReservation(member);
            item.Status = ItemStatus.Reserved;
        }

        // For printing summaries
        public IEnumerable<LibraryItem> AllItems() => _items.Values;
        public IEnumerable<LibraryMember> AllMembers() => _members.Values;
        public IEnumerable<Loan> AllLoans() => _loans;
    }

    // Demo program (Main)
    internal static class Program
    {
        private static void Main()
        {
            var today = DateOnly.FromDateTime(DateTime.Now);
            var lib = new Library();

            // Add items (6)
            lib.AddItem(new Book("B001", "The Pragmatic Programmer", "Andrew Hunt", 1999));
            lib.AddItem(new Book("B002", "Clean Code", "Robert C. Martin", 2008));
            lib.AddItem(new Magazine("M001", "Science Monthly", 42, 6, 2023));
            lib.AddItem(new Magazine("M002", "Gardening Today", 5, 3, 2022));
            lib.AddItem(new Audiobook("A001", "C# in Depth", "Jon Skeet", 720, 2019));
            lib.AddItem(new Audiobook("A002", "The Hobbit - Audio", "Andy Serkis", 700, 2012));

            // Register members (3)
            lib.RegisterMember(new RegularMember("U100", "Alice Johnson"));
            lib.RegisterMember(new RegularMember("U101", "Bob Smith"));
            lib.RegisterMember(new PremiumMember("U200", "Clara Oswald"));

            Console.WriteLine("Initial library items:");
            foreach (var item in lib.AllItems()) Console.WriteLine(item);

            Console.WriteLine("\nMembers:");
            foreach (var mem in lib.AllMembers()) Console.WriteLine(mem);

            Console.WriteLine("\n--- Borrow operations ---");
            try
            {
                // Successful borrows
                var loan1 = lib.BorrowItem("U100", "B001", today); // Alice borrows Pragmatic
                Console.WriteLine("Loan created: " + loan1);
                var loan2 = lib.BorrowItem("U101", "M001", today); // Bob borrows magazine
                Console.WriteLine("Loan created: " + loan2);
                var loan3 = lib.BorrowItem("U200", "A001", today); // Clara borrows audiobook
                Console.WriteLine("Loan created: " + loan3);

                // Reservation: Clara reserves a book currently available
                lib.ReserveItem("U200", "B002");
                Console.WriteLine("Clara reserved B002.");

                // Attempt: Alice attempts to borrow B002 which is reserved by Clara -> should fail
                try
                {
                    var failLoan = lib.BorrowItem("U100", "B002", today);
                    Console.WriteLine("Unexpected success: " + failLoan);
                }
                catch (CannotBorrowException ex)
                {
                    Console.WriteLine("Expected failure borrowing reserved item: " + ex.Message);
                }

                // Attempt: Bob tries to borrow more than allowed (simulate by borrowing multiple)
                lib.BorrowItem("U101", "B002", today); // Bob borrows B002 - NOTE: B002 was reserved by Clara, but Bob is borrowing to show failure. To demonstrate failure, cancel: instead try to make Bob borrow items until exceed.
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error during borrow sequence: " + ex.Message);
            }

            // Make Bob borrow more to reach limit
            try
            {
                lib.BorrowItem("U101", "B002", today); // attempts to borrow B002 (should fail if reserved) - wrap
            }
            catch (Exception ex)
            {
                Console.WriteLine("Expected/Handled: " + ex.Message);
            }

            // Show state
            Console.WriteLine("\nState after initial borrows and reservations:");
            foreach (var item in lib.AllItems()) Console.WriteLine(item);
            foreach (var mem in lib.AllMembers()) Console.WriteLine(mem);

            Console.WriteLine("\n--- Renew and Return operations ---");
            // Renew Clara's audiobook
            try
            {
                lib.RenewLoan("U200", "A001");
                Console.WriteLine("Clara renewed A001 successfully.");
                // Attempt second renew to fail
                try
                {
                    lib.RenewLoan("U200", "A001");
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Expected renew failure: " + ex.Message);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Renew error: " + ex.Message);
            }

            // Fast-forward time: simulate late return by Bob for magazine (due was earlier)
            var lateReturnDate = today.AddDays(30);
            try
            {
                var fine = lib.ReturnItem("U101", "M001", lateReturnDate);
                Console.WriteLine($"Bob returned M001 late. Fine: ${fine:F2}");
            }
            catch (Exception ex)
            {
                Console.WriteLine("Return error: " + ex.Message);
            }

            // Clara returns audiobook on time
            try
            {
                var fineClara = lib.ReturnItem("U200", "A001", today.AddDays(10));
                Console.WriteLine($"Clara returned A001. Fine: ${fineClara:F2}");
            }
            catch (Exception ex)
            {
                Console.WriteLine("Return error: " + ex.Message);
            }

            // Alice tries to borrow more than limit - create 5 borrows for Alice then one extra to fail
            try
            {
                // Add temporary items to borrow
                lib.AddItem(new Book("B100", "Temp Book 1", "Author X", 2010));
                lib.AddItem(new Book("B101", "Temp Book 2", "Author X", 2011));
                lib.AddItem(new Book("B102", "Temp Book 3", "Author X", 2012));
                lib.AddItem(new Book("B103", "Temp Book 4", "Author X", 2013));
                lib.AddItem(new Book("B104", "Temp Book 5", "Author X", 2014));
            }
            catch { /* ignore duplicates if re-run */ }

            try
            {
                lib.BorrowItem("U100", "B100", today);
                lib.BorrowItem("U100", "B101", today);
                lib.BorrowItem("U100", "B102", today);
                lib.BorrowItem("U100", "B103", today);
                lib.BorrowItem("U100", "B104", today); // now Alice at 6th borrow total - may exceed
            }
            catch (MaximumItemsReachedException ex)
            {
                Console.WriteLine("Expected max items reached for Alice: " + ex.Message);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Borrowing error for Alice: " + ex.Message);
            }

            // Search examples
            Console.WriteLine("\nSearch by title 'C#':");
            foreach (var i in lib.FindItemsByTitle("C#")) Console.WriteLine(i);

            Console.WriteLine("\nSearch by author/narrator 'Jon':");
            foreach (var i in lib.SearchByAuthorOrNarrator("Jon")) Console.WriteLine(i);

            // List overdue loans as of a far future date to capture overdue
            var checkDate = today.AddDays(40);
            var overdue = lib.ListOverdueLoans(checkDate).ToList();

            Console.WriteLine($"\n--- Overdue loans as of {checkDate} ---");
            if (!overdue.Any()) Console.WriteLine("No overdue loans.");
            foreach (var ov in overdue)
            {
                Console.WriteLine($"{ov.MemberName} ({ov.MemberId}) - {ov.ItemTitle} ({ov.ItemId}) Due: {ov.DueDate} Fine: ${ov.Fine:F2}");
            }

            Console.WriteLine("\n--- Summary ---");
            Console.WriteLine("Members:");
            foreach (var m in lib.AllMembers()) Console.WriteLine(m);
            Console.WriteLine("\nItems:");
            foreach (var it in lib.AllItems()) Console.WriteLine(it);
            Console.WriteLine("\nLoans:");
            foreach (var ln in lib.AllLoans()) Console.WriteLine(ln);

            Console.WriteLine("\nDemo finished.");
        }
    }
}