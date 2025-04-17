create table Dogs
(
    Id       int primary key identity (1,1),
    Name     nvarchar(max) not null,
    Birthday date          not null
);