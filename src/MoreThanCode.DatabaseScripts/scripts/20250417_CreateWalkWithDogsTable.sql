-- Create the Walks table
create table Walks
(
    Id int primary key identity (1,1),
);

-- Create the Coordinates table to store the path
create table WalkCoordinates
(
    Id            int primary key identity (1,1),
    WalkId        int not null,
    X             int not null,
    Y             int not null,
    SequenceOrder int not null, -- To maintain the order of coordinates in the path
    constraint FK_WalkCoordinates_Walks foreign key (WalkId) references Walks (Id)
);

-- Create the junction table for Dogs and Walks (many-to-many relationship)
create table WalkDogs
(
    WalkId int not null,
    DogId  int not null,
    constraint PK_WalkDogs primary key (WalkId, DogId),
    constraint FK_WalkDogs_Walks foreign key (WalkId) references Walks (Id),
    constraint FK_WalkDogs_Dogs foreign key (DogId) references Dogs (Id)
);

-- Create indexes for better query performance
create index IX_WalkCoordinates_WalkId on WalkCoordinates (WalkId);
create index IX_WalkDogs_DogId on WalkDogs (DogId);