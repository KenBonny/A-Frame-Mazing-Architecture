# A-Frame-Mazing Architecture: The Love Triangle of Software Design

Ever wondered what would happen if your code had a romantic comedy plotline? Welcome to A-Frame Architecture! Meet Infrastructure: the down-to-earth realist who talks to databases and juggles file systems, Logic: the pure-hearted idealist who makes all the decisions, and Controller: the matchmaker who keeps the other two from awkward encounters.

In this talk, we'll explore how to maintain this peculiar love triangle while keeping everyone happy and preventing Infrastructure from randomly crashing Logic's party with unexpected database calls. We'll dive into why Entity Framework doesn't need yet another repository wrapper, and why copying code might sometimes be better than creating the one ring... err... class to rule them all.

Warning: May contain dad jokes about dependency injection and mild rants about overengineering. No repositories were harmed in the making of this presentation.

---

# A-Frame-Mazing Architecture

A lot of architectures, patterns and code organisation promote splitting infrastructure code from (business) logic. Yet when I look at examples, both happen in the same function. They are separated by an interface, but they are interspersed. A-Frame Architecture helps me to separate them properly. In doing so, it simplifies a lot of scenarios and transforms easily testable code into trivially testable code.

Let's take a stroll through my dog walking app and get lost in all the good smells that emerge. I'll reward you with tasty treats... I mean patterns that help you fall into the pit of success. Possibly dug by my dog.

Disclaimer: no repositories were harmed in the making of this presentation.