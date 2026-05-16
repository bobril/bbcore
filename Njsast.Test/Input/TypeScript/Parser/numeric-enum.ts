enum Status {
    Pending,
    Running = 4,
    Done
}

console.log(Status.Pending, Status[4], Status.Done);
