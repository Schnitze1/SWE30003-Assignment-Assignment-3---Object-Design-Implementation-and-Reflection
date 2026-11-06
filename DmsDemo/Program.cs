using DmsDemo.Dms;
using DmsDemo.Dms.Ui;

if (args.Contains("--demo"))
{
    DmsDemo.RunDemo.Execute();
    return;
}

var reset = args.Contains("--reset");
var dms = new DeliveryManagementSystem(reset: reset);
var ui = new ConsoleUi(dms);
ui.Run();