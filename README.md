# SWE30003 Assignment Assignment 3 - Object Design Implementation and Reflection
NOTE: This Github Repo has been re-made/re-uploaded after issues with the write permissions to different branches and does not have a commit history because of this issue that our group faced.

# DMS Demo — Your Order We Deliver

SWE30003 Assignment 3 · Group 7

A terminal delivery management system written in **C# on .NET 8**. Standard library only.

## Run

```powershell
dotnet run --project DmsDemo

```

Commands
Command	What it does
dotnet build	Compile
dotnet test	Run the test suite (34 tests)
dotnet run --project DmsDemo	Start the app
dotnet run --project DmsDemo -- --reset	Re-seed demo data
dotnet run --project DmsDemo -- --demo	Run all six scenarios automatically

Platform
Windows 11
.NET 8 SDK
VS Code

Implemented areas
1. Log a delivery request (Task 1)
2. Quote, accept or reject (Task 2)
3. Allocate a driver, with availability (Tasks 3, 7)
4. Pickup with simulated payment (Task 5)
5. Delivery (Task 6)

Extras: track by reference (Task 9), cancel (Task 8), update contact (A26), in-system notifications (A14).

Account Info for Demo:
Office staff: O-001
Drivers: D-001, D-002, D-003
Customer: phone 0400000001 (Alice Smith)

Data
Stored in data/dms_data.json. Delete it or pass --reset to start clean.
