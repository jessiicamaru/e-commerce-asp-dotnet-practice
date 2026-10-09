# Data Model: Search suggestions while typing

No schema change. Products are read through the listing's search (the trigram indexes of specs/074); categories through
the existing read of all categories with their translations. Nothing is written.
