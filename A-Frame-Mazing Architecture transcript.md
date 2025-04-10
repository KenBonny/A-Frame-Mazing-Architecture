# A-Frame-Mazing Architecture

## Table of Content

1. What is A-Frame Architecture?

## What is A-Frame Architecture?

A-frame architecture is pretty simple: it separates interfacing with infrastructure from taking decisions using logic; between the two is a controller who orchestrates the flow of data and information. Everything in your code should resprect that separation.

![A-Frame Architecture triangle]("files://A-Frame Architecture triangle.png")

The idea behind this separation is that the infrastructure code will handle all side effects such as saving data to a database or file, sending requests over the network, publish events to a bus, etc. The logic code on the other hand should be a pure function that makes a decision based on the information passed to it and then communicates the result. The controller's job is to flow information and data from one to the other.



## Sources

[A-Frame Architecture with Wolverine](https://jeremydmiller.com/2023/07/19/a-frame-architecture-with-wolverine/)
[James Shore A-Frame Architecture](https://www.jamesshore.com/v2/projects/nullables/testing-without-mocks#a-frame-arch)